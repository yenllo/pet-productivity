namespace PetProductivity.Shared.Models;

public class ShopItem
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int Price { get; set; }
    public string Description { get; set; } = string.Empty;
    // No vacío = ítem de estilo de habitación (cosmético): su clave de fondo (forest/galaxy/bathroom/kitchen/loft).
    public string StyleKey { get; set; } = string.Empty;

    // --- F5.1 tienda real (campos aditivos, back-compat: server viejo → vacíos/defaults) ---
    // Categoría para los filtros de la tienda (ej "Muebles", "Decoración", "Vida", "Estilos", "Consumibles").
    public string Category { get; set; } = string.Empty;
    // Clave del sprite del objeto en Resources/Raw (obj_<id>_<vista>), vacío = usa emoji/Icon. Aprovecha
    // las mismas piezas que el diorama; la vista concreta se elige al colocar (F5.2).
    public string SpriteId { get; set; } = string.Empty;
    // Moneda: "Gold" (ganado jugando) o "Premium" (dinero real, F5.4). El oro NUNCA compra Premium.
    public string Currency { get; set; } = "Gold";
    // Rareza para el badge de la tarjeta: Common/Rare/Unique/Event/Collab.
    public string Rarity { get; set; } = "Common";

    // --- F5.3 eventos: ventana de disponibilidad. null = siempre disponible. ---
    public DateTime? AvailableFrom { get; set; }
    public DateTime? AvailableTo { get; set; }

    // Origen para colaboraciones (crédito en la tarjeta), ej "Colab: <marca>". Vacío = ninguno.
    public string Source { get; set; } = string.Empty;

    // --- T19-C: efecto declarativo del ítem (consumibles). Vacío = sin efecto (cosmético). ---
    // Soportados hoy: "heal" (EffectValue = HP). El despacho es un switch en ShopController;
    // añadir un consumible nuevo = declarar su efecto en el info.json, no tocar C#.
    public string Effect { get; set; } = string.Empty;
    public int EffectValue { get; set; }

    // --- Colocación en el cuarto (info.json "footprint"/"slot"; aditivos, server viejo → defaults) ---
    // Huella W×D en celdas, en la orientación de la vista por defecto (_l). 1×1 si no se declara.
    public int GridW { get; set; } = 1;
    public int GridD { get; set; } = 1;
    // "wall" = se cuelga en las 2 paredes traseras del cuarto; vacío = objeto de piso.
    public string Slot { get; set; } = string.Empty;
}
