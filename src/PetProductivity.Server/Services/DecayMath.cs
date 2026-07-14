using PetProductivity.Shared.Models;

namespace PetProductivity.Server.Services;

/// <summary>
/// T10: decadencia LAZY — el estado del juego ya no depende del uptime de Render. `Pet.LastDecayAt`
/// registra hasta cuándo se aplicó la regla; este método puro aplica los ticks acumulados (2 h c/u)
/// al materializar la mascota (GET usuario, premiar) y en el barrido best-effort (push de T2).
/// Idempotente entre lazy y barrido porque ambos comparten LastDecayAt. Llamar SIEMPRE con el
/// PetWriteLock tomado y la entidad recargada (si no, requests concurrentes duplican ticks).
/// </summary>
public static class DecayMath
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromHours(2);
    public const int HungerPerTick = 5;
    public const int StarvingDamagePerTick = 3; // T26: cristal a ~5.5 días de abandono

    // T3-E: escudo de ausencia. Con el dueño ausente (sin actividad hace más de estos días), la
    // mascota entra en "sueño profundo": la decadencia solo corre los primeros días tras la última
    // actividad y lo dormido SE PERDONA (el reloj salta a ahora). Distingue "se fue" (no castigar:
    // ya no le duele; vuelve a una mascota viva y hambrienta) de "está activo y la ignora" (castigar).
    public const int AbsenceSleepDays = 3;

    /// <summary>
    /// Variante con escudo de ausencia. lastActivity es el token de día local de T1 (no un instante
    /// UTC exacto); el desfase de horas es irrelevante para un umbral de 3 días. Null = sin registro
    /// de actividad → regla normal.
    /// </summary>
    public static int ApplyPendingDecay(Pet pet, DateTime utcNow, DateTime? lastActivity)
    {
        var sleepFrom = lastActivity?.AddDays(AbsenceSleepDays);
        if (sleepFrom == null || sleepFrom >= utcNow)
            return ApplyPendingDecay(pet, utcNow); // dueño activo: regla normal

        var applied = ApplyPendingDecay(pet, sleepFrom.Value); // decae solo hasta dormirse
        pet.LastDecayAt = utcNow;                              // lo dormido se perdona
        return applied;
    }

    // T24#4: decadencia COLECTIVA de la mascota de grupo. A diferencia de la personal (que decae
    // siempre y solo "duerme" tras la ausencia), la compartida NO pasa hambre mientras CUALQUIER
    // miembro esté activo (últimos GroupIdleDays); solo empieza a decaer cuando TODO el grupo lleva
    // inactivo más que eso. Un grupo activo protege a la mascota incluso de la deuda vieja (el reloj
    // salta a ahora), así nadie vuelve a una compartida muerta por descuido de otro día.
    public const int GroupIdleDays = 3;

    /// <summary>groupLastActivity = la actividad MÁS reciente entre los miembros (null = nadie hizo
    /// nada aún → la mascota espera, sin castigo). Devuelve ticks aplicados.</summary>
    public static int ApplyGroupDecay(Pet pet, DateTime utcNow, DateTime? groupLastActivity)
    {
        var idleSince = groupLastActivity?.AddDays(GroupIdleDays);
        if (idleSince == null || idleSince >= utcNow)
        {
            pet.LastDecayAt = utcNow; // grupo activo (o sin actividad aún): cuidada, sin deuda
            return 0;
        }
        // Todo el grupo inactivo hace rato: la deuda cuenta desde que empezó la inactividad, no antes.
        if (pet.LastDecayAt == null || pet.LastDecayAt < idleSince) pet.LastDecayAt = idleSince;
        return ApplyPendingDecay(pet, utcNow);
    }

    /// <summary>
    /// ¿Mutaría algo <see cref="ApplyPendingDecay(Pet, DateTime, DateTime?)"/>? Función pura y sin BD.
    /// Permite al GET del usuario saltarse el lock + ReloadAsync + SaveChanges cuando no hay nada que
    /// aplicar — el caso común (dueño activo que abrió la app hace menos de un tick). Ese endpoint corre
    /// en cada arranque de la app y tras cada acción, y pagaba 2-3 round-trips a la BD para no hacer nada.
    /// Conservador: ante la duda devuelve true y se toma el camino lento (correcto) de siempre.
    /// </summary>
    public static bool IsDecayPending(Pet pet, DateTime utcNow, DateTime? lastActivity)
    {
        if (pet.LastDecayAt == null) return true;              // hay que inicializar el reloj
        if (pet.Status == PetStatus.Crystallized) return true; // el reloj salta a ahora (escribe)

        var sleepFrom = lastActivity?.AddDays(AbsenceSleepDays);
        if (sleepFrom != null && sleepFrom < utcNow) return true; // ausente: lo dormido se perdona (escribe)

        return utcNow - pet.LastDecayAt.Value >= TickInterval;  // ¿hay al menos un tick que aplicar?
    }

    /// <summary>Aplica los ticks pendientes. Devuelve cuántos aplicó (0 también cuando recién inicializa).</summary>
    public static int ApplyPendingDecay(Pet pet, DateTime utcNow)
    {
        if (pet.LastDecayAt == null)
        {
            // Primer contacto (mascota pre-migración o recién nacida): el reloj parte AHORA —
            // nada de decadencia retroactiva por días que el sistema no medía.
            pet.LastDecayAt = utcNow;
            return 0;
        }
        if (pet.Status == PetStatus.Crystallized)
        {
            pet.LastDecayAt = utcNow; // una piedra no decae; el reloj no acumula deuda
            return 0;
        }

        var ticks = (int)((utcNow - pet.LastDecayAt.Value) / TickInterval);
        if (ticks <= 0) return 0;

        for (int i = 0; i < ticks && pet.Status != PetStatus.Crystallized; i++)
        {
            pet.Hunger -= HungerPerTick;
            if (pet.Hunger < 0)
            {
                pet.Hunger = 0;
                pet.ApplyDamage(StarvingDamagePerTick); // respeta el escudo de gracia internamente
            }
        }
        // Avanza el reloj tick a tick (conserva el resto <2 h para el próximo cómputo).
        pet.LastDecayAt = pet.LastDecayAt.Value.AddTicks(TickInterval.Ticks * ticks);
        return ticks;
    }
}
