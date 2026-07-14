namespace PetProductivity.Shared.Models;

// 2. SEMÁFORO DE SINCRONIZACIÓN
public enum SyncStatus
{
    Available, // 🟢 LFG: "Avísame si alguien trabaja"
    Working,   // 🔨 Trabajando: Detona el combo
    Busy,      // 🔴 Ocupado: No molestar
    Offline    // ⚫ Desconectado
}
