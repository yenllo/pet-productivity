using System.Text.Json.Serialization;

namespace PetProductivity.Shared.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public SyncStatus CurrentStatus { get; set; } = SyncStatus.Offline;
    
    // Relación con su mascota
    public Pet? UserPet { get; set; }

    // Inventario (ItemName -> Quantity)
    public Dictionary<string, int> Inventory { get; set; } = new();

    // Stats Globales
    public int CurrentStreak { get; set; } = 0;
    // T1: racha diaria REAL — día local en que hubo actividad por última vez (token de LocalDay)
    // y máximo histórico. La racha se rompe si pasa un día sin tarea/foco (el congelador salva 1 día).
    public DateTime? LastActivityDate { get; set; }
    public int MaxStreak { get; set; }
    // T2: anti-spam de push — último día (local) en que se envió cada tipo de aviso.
    public Dictionary<string, DateTime> LastNotifications { get; set; } = new();
    public int TotalTasksCompleted { get; set; } = 0;

    // Stats de modo foco (AC3 v2): racha de días con foco + minutos totales enfocados.
    public int TotalFocusMinutes { get; set; } = 0;
    public int FocusStreak { get; set; } = 0;
    public int MaxFocusStreak { get; set; } = 0;
    public DateTime? LastFocusDate { get; set; }

    // T8: zona horaria IANA del usuario (el cliente la manda al login/registro/upgrade). Define su
    // "hoy" (ritual, rendimientos, rachas) vía LocalDay del server. Vacío = default (Chile).
    public string TimeZoneId { get; set; } = string.Empty;

    // Daily Rituals
    public string RitualGridState { get; set; } = "0,0,0,0,0,0,0,0,0";
    public DateTime LastRitualReset { get; set; } = DateTime.MinValue; // Start with MinValue so it triggers a reset on first use
    public double ActiveXpMultiplier { get; set; } = 1.0;
    // T7: nombres de las 9 celdas del ritual, separados por '|' ("" = defaults del cliente).
    // Convierte el 3-en-raya en el tablero de hábitos PERSONAL: la línea = "hice 3 de mis hábitos".
    public string RitualLabels { get; set; } = string.Empty;

    // Auth
    public string Email { get; set; } = string.Empty;
    // Nunca serializar el hash al cliente (defensa en profundidad; hoy se vacía a mano en cada respuesta).
    // El cliente nunca envía User, así que ignorarlo en ambos sentidos es seguro.
    [JsonIgnore] public string Password { get; set; } = string.Empty;

    // Sync
    public DateTime LastSync { get; set; } = DateTime.UtcNow;

    // Preferences
    public string ThemePreference { get; set; } = "System"; // System, Light, Dark
    public bool NotificationsEnabled { get; set; } = true;

    // Estilo de habitación equipado (cosmético, comprado con oro). "default" = sala base.
    public string ActiveRoomStyle { get; set; } = "default";

    // Muebles colocados en el cuarto (F5.2, tipo Sims). Cosmético: el server solo valida propiedad
    // (cada Name debe estar en Inventory); la grilla/colisión es UX del cliente. Vacío = seed por defecto.
    public List<PlacedFurniture> PlacedFurniture { get; set; } = new();

    // T4-A: legado de mascotas retiradas al llegar a Maestro (columna JSON, como PlacedFurniture).
    // Cada entrada es un capítulo de la vida productiva del usuario; nadie borra eso.
    public List<RetiredPet> RetiredPets { get; set; } = new();

    // Token FCM del dispositivo (para push con app cerrada). Null = sin dispositivo registrado.
    public string? DeviceToken { get; set; }
}
