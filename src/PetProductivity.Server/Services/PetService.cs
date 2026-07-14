using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using PetProductivity.Shared.Models;
using PetProductivity.Server.Data;
using PetProductivity.Server.Hubs;

namespace PetProductivity.Server.Services;

public class PetService
{
    private readonly AppDbContext _context;
    private readonly AiJudgeService _aiJudge;
    private readonly ILogger<PetService> _logger;
    private readonly PresenceService _presence;
    private readonly IHubContext<FamilyHub> _hub;
    private readonly PetWriteLock _petLock;
    private readonly PushService _push;

    public PetService(AppDbContext context, AiJudgeService aiJudge, ILogger<PetService> logger,
        PresenceService presence, IHubContext<FamilyHub> hub, PetWriteLock petLock, PushService push)
    {
        _context = context;
        _aiJudge = aiJudge;
        _logger = logger;
        _presence = presence;
        _hub = hub;
        _petLock = petLock;
        _push = push;
    }

    // Back-compat: registra a la mascota personal, sin confirmación.
    public Task<TaskResult> ProcessTaskCompletion(Guid userId, string taskDescription)
        => ProcessTaskCompletion(userId, Guid.Empty, taskDescription, true);

    public async Task<TaskResult> ProcessTaskCompletion(Guid userId, Guid petId, string taskDescription, bool confirmed, int? timedDifficulty = null,
        double rewardMultiplier = 1.0, Guid? proofId = null, string proofVerdict = "none", bool immediateShared = false,
        string language = "es")
    {
        var user = await _context.Users
            .Include(u => u.UserPet)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            _logger.LogWarning("User not found for {UserId}", userId);
            return new TaskResult { Message = "Usuario no encontrado" };
        }

        // Resolver + autorizar la mascota destino: personal, o compartida de un grupo del usuario.
        Pet? pet;
        bool isShared = false;
        Guid groupId = Guid.Empty;
        if (petId == Guid.Empty || (user.UserPet != null && user.UserPet.Id == petId))
        {
            pet = user.UserPet;
        }
        else
        {
            var sharedPet = await _context.SharedPets.FirstOrDefaultAsync(p => p.Id == petId);
            groupId = await _context.Groups.Where(g => g.SharedPetId == petId).Select(g => g.Id).FirstOrDefaultAsync();
            bool isMember = groupId != Guid.Empty &&
                await _context.GroupMemberships.AnyAsync(m => m.GroupId == groupId && m.UserId == userId);
            if (sharedPet == null || !isMember)
                return new TaskResult { Message = "No puedes registrar tareas a esta mascota." };
            pet = sharedPet;
            isShared = true;
        }
        if (pet == null)
            return new TaskResult { Message = "Usuario o mascota no encontrados." };

        // Mascota de grupo dormida: necesita ≥2 miembros para funcionar.
        if (isShared)
        {
            var memberCount = await _context.GroupMemberships.CountAsync(m => m.GroupId == groupId);
            if (memberCount < 2)
                return new TaskResult { Message = $"{pet.Name} está dormida. Necesita al menos 2 miembros.", PetName = pet.Name };
            // El huevo de grupo aún no nace: no recibe tareas hasta el voto unánime.
            if (pet is SharedPet sharedForHatch && !sharedForHatch.IsHatched)
                return new TaskResult { Message = $"{pet.Name} aún es un huevo. Todos deben tocar \"Hacer nacer\".", PetName = pet.Name };
        }

        // 1. AI Judgment (una sola llamada: juicio + feedback en español — T12)
        var (difficulty, category, relevant, plausibility, feedback) = await _aiJudge.EvaluateTaskAsync(taskDescription, pet.CurrentArchetype, language);

        // Modo foco (AC3): la dificultad la fija el TIEMPO real medido por el server, no el texto.
        // Plausibilidad 10 porque está verificado por tiempo (la categoría sí sale de la IA).
        if (timedDifficulty.HasValue)
        {
            difficulty = Math.Clamp(timedDifficulty.Value, 1, 10);
            plausibility = 10;
        }

        // 2. Cristalizada: intento de revivir (no se bloquea por relevancia).
        if (pet.Status == PetStatus.Crystallized)
        {
            // T3-F: la hazaña épica (9+) sigue rompiendo el cristal al instante.
            bool revived = pet.TryRevive(difficulty);
            bool viaGrietas = false;
            // T3-A: esfuerzo real (foco completado, o tarea ≥5) en días distintos agrieta el cristal.
            if (!revived && (timedDifficulty.HasValue || difficulty >= Pet.RevivalCreditDifficulty))
                revived = viaGrietas = pet.AddRevivalCredit(LocalDay.TodayTokenFor(user));
            await _context.SaveChangesAsync();

            if (revived)
                return new TaskResult
                {
                    Message = viaGrietas
                        ? "¡Tres días de constancia rompieron el cristal! Tu mascota vive de nuevo."
                        : "¡La energía de tu esfuerzo ha roto el cristal! Tu mascota vive de nuevo.",
                    IsRevived = true, DifficultyScore = difficulty, PetName = pet.Name
                };

            var grietas = pet.RevivalProgress > 0
                ? $" El cristal lleva {pet.RevivalProgress}/{Pet.RevivalDaysNeeded} grietas — vuelve otro día con más esfuerzo."
                : "";
            return new TaskResult
            {
                Message = $"El esfuerzo fue noble (Dificultad {difficulty}), pero el cristal pide más: " +
                          $"una hazaña épica (9+), o esfuerzo real (foco o tarea 5+) durante {Pet.RevivalDaysNeeded} días distintos.{grietas}",
                IsRevived = false, DifficultyScore = difficulty, PetName = pet.Name
            };
        }

        // 3. Compuerta de relevancia (solo mascotas de grupo; la personal Neutral es catch-all).
        if (isShared && !relevant && !confirmed)
        {
            return new TaskResult
            {
                NeedsConfirmation = true,
                DifficultyScore = difficulty,
                StatCategory = category,
                PetName = pet.Name,
                Message = $"Esta tarea no parece del contexto de {pet.Name}. ¿Registrarla igual con recompensa reducida?"
            };
        }

        // 4. Recompensas: el cálculo puro vive en RewardMath (T15-A/T19-A); aquí solo se reúnen los hechos.
        bool reduced = isShared && !relevant; // confirmada pese a no encajar el contexto

        // Dedupe (anti-trampa): misma descripción (normalizada) en 24 h. NO aplica al foco:
        // está verificado por tiempo real (repetir "leer" cada día es legítimo; el spam de texto no).
        bool duplicate = false;
        if (!timedDifficulty.HasValue)
        {
            var since = DateTime.UtcNow.AddHours(-24);
            var norm = Normalize(taskDescription);
            var recentDescs = await _context.TaskItems
                .Where(t => t.UserId == userId && t.CreatedAt >= since)
                .Select(t => t.Description).ToListAsync();
            duplicate = recentDescs.Any(d => Normalize(d) == norm);
        }

        // Rendimientos decrecientes: cuántas tareas lleva hoy (hoy LOCAL del usuario — T8). El FOCO
        // queda exento (T26): está verificado por tiempo real — castigarlo desincentiva lo que queremos.
        var startOfToday = LocalDay.StartOfTodayUtc(user);
        var todayCount = timedDifficulty.HasValue ? 0 : await _context.TaskItems
            .CountAsync(t => t.UserId == userId && t.CreatedAt >= startOfToday);

        // Frenesí: ×2 XP si la mascota de grupo tiene ≥2 miembros "Trabajando" ahora mismo.
        bool frenzy = isShared && _presence.IsFrenzyActive(groupId);

        var (xpEarned, goldEarned) = RewardMath.Compute(difficulty, plausibility, user.ActiveXpMultiplier,
            reduced, duplicate, todayCount, frenzy, rewardMultiplier);

        if (difficulty >= RewardMath.RitualResetDifficulty && user.ActiveXpMultiplier > 1.0)
            user.ActiveXpMultiplier = 1.0;

        // AC4: las tareas de mascota de GRUPO no premian al instante → validación de la familia.
        // Excepción: el FOCO grupal está verificado por tiempo → premia al instante (immediateShared).
        if (isShared && !immediateShared)
        {
            _context.TaskApprovals.Add(new TaskApproval
            {
                GroupId = groupId,
                PetId = pet.Id,
                RequesterUserId = userId,
                Description = taskDescription,
                Difficulty = difficulty,
                Category = category,
                XpEarned = xpEarned,
                GoldEarned = goldEarned
            });
            await _context.SaveChangesAsync();
            await _hub.Clients.Group(FamilyHub.Room(groupId)).SendAsync("TaskPending", groupId);
            _logger.LogInformation("Group task pending. Pet={Pet} Requester={User} XP={XP}", pet.Name, userId, xpEarned);

            // T6-D: la causa raíz de la validación muerta es que nadie se entera — push a los demás
            // miembros (máx. 1/día por usuario vía NotificationPolicy; los detalles se ven en la app).
            var otherIds = await _context.GroupMemberships
                .Where(m => m.GroupId == groupId && m.UserId != userId).Select(m => m.UserId).ToListAsync();
            var others = await _context.Users.Where(x => otherIds.Contains(x.Id)).ToListAsync();
            var notify = new List<Guid>();
            foreach (var o in others)
                if (NotificationPolicy.ShouldSend(o, "approval"))
                {
                    NotificationPolicy.MarkSent(o, "approval");
                    notify.Add(o.Id);
                }
            if (notify.Count > 0)
            {
                await _context.SaveChangesAsync();
                var desc = taskDescription.Length > 40 ? taskDescription[..40] + "…" : taskDescription;
                await _push.SendToUsersAsync(notify, "🗳️ Tu familia espera tu voto",
                    $"{user.Username} hizo «{desc}» — tu aprobación premia a {pet.Name}.");
            }
            return new TaskResult
            {
                Message = $"Tarea enviada a validación de la familia. Al aprobarla, {pet.Name} ganará {xpEarned} XP.",
                DifficultyScore = difficulty,
                StatCategory = category,
                PetName = pet.Name,
                NewTotalXp = pet.TotalXp,
                WasReducedReward = reduced,
                EmotionalFeedback = feedback
            };
        }

        // Mascota personal: recompensa inmediata.
        var taskItem = await ApplyRewardAsync(pet, user, userId, difficulty, category, xpEarned, goldEarned, taskDescription, groupId, frenzy, reduced, proofId, proofVerdict);
        return new TaskResult
        {
            TaskId = taskItem.Id,
            Message = reduced
                ? $"Registrada fuera de contexto. Ganaste {xpEarned} XP y {goldEarned} Oro (reducido)."
                : $"Tarea completada. Ganaste {xpEarned} XP y {goldEarned} Oro.",
            XpEarned = xpEarned,
            GoldEarned = goldEarned,
            DifficultyScore = difficulty,
            StatCategory = category,
            PetName = pet.Name,
            NewTotalXp = pet.TotalXp,
            IsRevived = false,
            WasReducedReward = reduced,
            EmotionalFeedback = feedback
        };
    }

    // Aplica la recompensa al pet (extraído para reutilizar en la validación social AC4).
    // Devuelve el TaskItem creado para que el TaskId de la respuesta sea el real (T11-D3).
    private async Task<TaskItem> ApplyRewardAsync(Pet pet, User user, Guid userId, int difficulty, string category,
        int xpEarned, int goldEarned, string taskDescription, Guid groupId, bool frenzy = false, bool reduced = false,
        Guid? proofId = null, string proofVerdict = "none")
    {
        // Serializa el read-modify-write de ESTA mascota (anti lost-update con premios simultáneos).
        // El lock NO cubre la llamada a la IA (ya ocurrió antes); solo el reload+apply+save (operaciones rápidas).
        using var _ = await _petLock.AcquireAsync(pet.Id);
        await _context.Entry(pet).ReloadAsync(); // valores frescos de oro/HP/afecto antes de incrementar

        // T10: saldar la decadencia pendiente ANTES de premiar (dentro del lock, tras el reload).
        // T3-E: con el LastActivityDate VIEJO (DailyStreak.Advance corre después) — quien vuelve de
        // una ausencia larga paga solo 3 días de decadencia, no el cadáver completo.
        DecayMath.ApplyPendingDecay(pet, DateTime.UtcNow, user.LastActivityDate);

        pet.GoldCoins += goldEarned;
        pet.Hunger = Math.Min(100, pet.Hunger + difficulty * RewardMath.HungerPerDifficulty);
        pet.Heal(difficulty * RewardMath.HealPerDifficulty);

        var stageBefore = pet.EvolutionStage; // T4-E: detectar el salto de etapa para celebrarlo
        pet.AddStatXp(category, xpEarned);
        var evolvedTo = pet.EvolutionStage > stageBefore ? pet.EvolutionStage : (EvolutionStage?)null;

        if (pet is SharedPet sp)
            sp.UpdateAffection(userId, true);

        user.TotalTasksCompleted++;
        DailyStreak.Advance(user, LocalDay.TodayTokenFor(user)); // T1: racha diaria real (tarea o foco)

        var item = new TaskItem
        {
            UserId = userId,
            PetId = pet.Id,
            Description = taskDescription,
            IsCompleted = true,
            AiDifficultyScore = difficulty,
            AiStatCategory = category,
            XpEarned = xpEarned,
            GoldEarned = goldEarned,
            CreatedAt = DateTime.UtcNow,
            ProofId = proofId,
            ProofVerdict = proofVerdict
        };
        _context.TaskItems.Add(item);

        CheckEvolution(pet);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Reward applied. Pet={Pet} XP={XP} Gold={Gold} Cat={Category} Reduced={Reduced} Frenzy={Frenzy}", pet.Name, xpEarned, goldEarned, category, reduced, frenzy);

        // T4-E: el pico de dopamina que hoy ocurre en silencio. Push del hito (política T2 anti-spam,
        // tipo por etapa para que las 3 evoluciones puedan avisar; la ceremonia in-app la dispara el
        // cliente comparando la etapa persistida). Solo mascota personal: la de grupo la celebra su flujo.
        if (evolvedTo is { } stage && pet is not SharedPet && NotificationPolicy.ShouldSend(user, $"evo_{(int)stage}"))
        {
            NotificationPolicy.MarkSent(user, $"evo_{(int)stage}");
            await _context.SaveChangesAsync();
            await _push.SendToUsersAsync(new[] { user.Id },
                $"✨ ¡{pet.Name} evolucionó!", $"Ahora es {StageNameEs(stage)}. Tu constancia lo hizo crecer.");
        }

        if (pet is SharedPet shared && groupId != Guid.Empty)
            await _hub.Clients.Group(FamilyHub.Room(groupId)).SendAsync("PetUpdate", groupId, PetStateDto.From(shared));
        return item;
    }

    private static string StageNameEs(EvolutionStage s) => s switch
    {
        EvolutionStage.Baby => "Cría",
        EvolutionStage.Adult => "Adulto",
        EvolutionStage.Master => "Maestro",
        _ => "Huevo"
    };

    // AC4: aprobar una tarea pendiente de grupo; al llegar a mayoría de los OTROS miembros, aplica la recompensa.
    public async Task<(bool approved, int votes, int needed)> ApproveTaskAsync(Guid approvalId, Guid approverId)
    {
        // T11-D2: serializa leer→votar→premiar→borrar de ESTA aprobación. Sin esto, dos votos
        // concurrentes se pisan (last-write-wins en Approvals = voto perdido, reproducido en T21)
        // y el double-tap revienta en 500. Reutiliza PetWriteLock (lock genérico por Guid).
        using var _ = await _petLock.AcquireAsync(approvalId);
        var ta = await _context.TaskApprovals.FirstOrDefaultAsync(t => t.Id == approvalId);
        if (ta == null) throw new GroupException(404, "Tarea no encontrada");

        var members = await _context.GroupMemberships
            .Where(m => m.GroupId == ta.GroupId).Select(m => m.UserId).ToListAsync();
        if (!members.Contains(approverId)) throw new GroupException(403, "No eres miembro de este grupo");

        // El solicitante no se auto-aprueba; solo cuentan los OTROS miembros.
        if (approverId != ta.RequesterUserId && !ta.Approvals.Contains(approverId))
            ta.Approvals.Add(approverId);

        int others = members.Count(m => m != ta.RequesterUserId);
        int needed = others / 2 + 1;                 // mayoría estricta de los otros
        int votes = ta.Approvals.Count;
        bool approved = others >= 1 && votes >= needed;

        if (approved)
        {
            await ApplyApprovalAsync(ta);
        }
        else
        {
            await _context.SaveChangesAsync();
        }
        return (approved, votes, needed);
    }

    // Aplica una aprobación resuelta (por mayoría o por timeout T6): premia, borra la fila y difunde.
    private async Task ApplyApprovalAsync(TaskApproval ta)
    {
        var pet = await _context.SharedPets.FirstOrDefaultAsync(p => p.Id == ta.PetId);
        var requester = await _context.Users.FirstOrDefaultAsync(u => u.Id == ta.RequesterUserId);
        if (pet != null && requester != null)
            await ApplyRewardAsync(pet, requester, ta.RequesterUserId, ta.Difficulty, ta.Category,
                ta.XpEarned, ta.GoldEarned, ta.Description, ta.GroupId);
        _context.TaskApprovals.Remove(ta);
        await _context.SaveChangesAsync();
        await _hub.Clients.Group(FamilyHub.Room(ta.GroupId)).SendAsync("TaskApproved", ta.GroupId);
    }

    /// <summary>T6-B: el silencio de la familia otorga — aplica una aprobación vencida con los valores
    /// ya guardados en la fila. Idempotente: lock + re-fetch (si un voto o otro tick la resolvió, no-op).</summary>
    public async Task<bool> AutoApproveExpiredAsync(Guid approvalId)
    {
        using var _ = await _petLock.AcquireAsync(approvalId);
        var ta = await _context.TaskApprovals.FirstOrDefaultAsync(t => t.Id == approvalId);
        if (ta == null) return false;
        await ApplyApprovalAsync(ta);
        _logger.LogInformation("Auto-approved expired task. Approval={Id} XP={XP}", approvalId, ta.XpEarned);
        return true;
    }

    // Normaliza para comparar descripciones (minúsculas + colapsa espacios) en el dedupe anti-trampa.
    private static string Normalize(string s) =>
        string.Join(' ', (s ?? string.Empty).ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // El stat dominante debe superar al segundo por este margen para re-especializar el arquetipo.
    private const double DominantStatMargin = 1.2;

    public void CheckEvolution(Pet pet)
    {
        if (pet.CurrentArchetype == Archetype.Neutral) return;
        if (pet.Stats.Count == 0) return;

        // Find dominant stat
        var maxStat = pet.Stats.MaxBy(kvp => kvp.Value);
        double totalStats = pet.Stats.Values.Sum();
        double otherStatsMax = pet.Stats.Where(k => k.Key != maxStat.Key).Select(k => k.Value).DefaultIfEmpty(0).Max();

        // Rule: Dominant stat must be 20% higher than the next highest stat to trigger evolution change
        // Or simply if it's the highest by a margin. 
        // Let's implement the user's rule: "supera en un 20% al resto" (exceeds others by 20%)
        
        if (maxStat.Value > otherStatsMax * DominantStatMargin)
        {
            // Map the dominant stat back to the archetype that owns it (single source: ArchetypeStats).
            // If the stat isn't owned by any archetype (e.g. "General"), keep the current archetype
            // instead of wrongly defaulting to Scholar.
            var owningArchetype = ArchetypeStats.GetArchetypeForStat(maxStat.Key);
            if (owningArchetype.HasValue && pet.CurrentArchetype != owningArchetype.Value)
            {
                pet.CurrentArchetype = owningArchetype.Value;
                // Maybe trigger an event or notification here
            }
        }
    }
    // null = usuario no encontrado. T20-I1b: antes devolvía el string literal "User not found" y el
    // controller lo comparaba a mano — un cambio de tilde en cualquiera de los dos lados rompe el 404
    // en silencio (200 con ese texto como si fuera el estado del ritual). Nulable = no hay forma de que
    // el compilador deje pasar esa desincronización.
    public async Task<string?> ToggleRitualCell(Guid userId, int cellIndex)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return null;

        var today = LocalDay.TodayTokenFor(user);   // T8: el ritual se resetea a medianoche LOCAL
        var stateStr = user.RitualGridState ?? "0,0,0,0,0,0,0,0,0";
        // Parseo defensivo: una cadena legada/corrupta no debe tirar 500.
        var parsed = stateStr.Split(',');
        var state = new int[9];
        if (parsed.Length == 9)
            for (int i = 0; i < 9; i++) int.TryParse(parsed[i], out state[i]);

        // 1. Check Reset (New Day)
        if (user.LastRitualReset < today)
        {
            // Reset grid
            state = new int[9]; // All 0
            user.LastRitualReset = today;
            user.ActiveXpMultiplier = 1.0; 
        }

        // 2. Toggle Cell
        if (cellIndex >= 0 && cellIndex < 9)
        {
            state[cellIndex] = state[cellIndex] == 0 ? 1 : 0;
        }

        // 3. Check for Win (Tic-Tac-Toe Lines)
        bool hasLine = CheckLine(state, 0, 1, 2) || CheckLine(state, 3, 4, 5) || CheckLine(state, 6, 7, 8) || // Rows
                       CheckLine(state, 0, 3, 6) || CheckLine(state, 1, 4, 7) || CheckLine(state, 2, 5, 8) || // Cols
                       CheckLine(state, 0, 4, 8) || CheckLine(state, 2, 4, 6);        // Diags

        if (hasLine)
        {
            user.ActiveXpMultiplier = RewardMath.RitualMultiplier;
        }
        else
        {
            user.ActiveXpMultiplier = 1.0;
        }

        // Save
        user.RitualGridState = string.Join(",", state);
        await _context.SaveChangesAsync();

        return user.RitualGridState;
    }

    private bool CheckLine(int[] s, int a, int b, int c)
    {
        return s[a] == 1 && s[b] == 1 && s[c] == 1;
    }
}
