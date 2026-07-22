using PetProductivity.Shared.Models;

namespace PetProductivity.Server.Services;

/// <summary>
/// T1: racha diaria real y unificada — cualquier recompensa aplicada (tarea de texto o foco, ambas
/// pasan por ApplyRewardAsync) cuenta como actividad del día; el ritual queda excluido (trampeable).
/// Se evalúa lazy (al premiar); el aviso proactivo "tu racha muere hoy" es de T2.
/// </summary>
public static class DailyStreak
{
    // Clave del inventario = nombre exacto del ítem en Catalog/Consumibles.
    public const string FreezerItem = "Congelador de Racha";

    /// <summary>Avanza la racha para el día local (token de LocalDay). Devuelve true si consumió un congelador.</summary>
    public static bool Advance(User u, DateTime today)
    {
        var last = u.LastActivityDate?.Date;
        if (last == today)
        {
            // "Hoy ya contó" — pero desde 2026-07-22 el registro también estampa LastActivityDate
            // (ancla del escudo T3-E) SIN arrancar racha. La primera tarea de ese mismo día debe
            // seguir valiendo racha 1; sin este guard quedaba clavada en 0 hasta el día siguiente.
            if (u.CurrentStreak == 0)
            {
                u.CurrentStreak = 1;
                u.MaxStreak = Math.Max(u.MaxStreak, 1);
            }
            return false;
        }

        bool froze = false;
        if (last == today.AddDays(-1))
        {
            u.CurrentStreak++;
        }
        else if (last == today.AddDays(-2) && TryConsumeFreezer(u))
        {
            froze = true;        // ayer no hubo nada, pero el congelador lo cubre
            u.CurrentStreak++;
        }
        else
        {
            u.CurrentStreak = 1; // hueco sin protección (o primera actividad de la cuenta)
        }
        u.MaxStreak = Math.Max(u.MaxStreak, u.CurrentStreak);
        u.LastActivityDate = today;
        return froze;
    }

    // Huecos de más de 1 día no se congelan (y el ítem no se desperdicia).
    private static bool TryConsumeFreezer(User u)
    {
        if (u.Inventory == null || !u.Inventory.TryGetValue(FreezerItem, out var n) || n <= 0) return false;
        if (n == 1) u.Inventory.Remove(FreezerItem);
        else u.Inventory[FreezerItem] = n - 1;
        return true;
    }
}
