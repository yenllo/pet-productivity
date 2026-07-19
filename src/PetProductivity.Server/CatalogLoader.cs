using System.Text.Json;
using PetProductivity.Shared.Models;

namespace PetProductivity.Server;

// Carga el catálogo de la tienda desde la carpeta Catalog/ (una subcarpeta por objeto, cada una con su
// info.json). Es la ÚNICA fuente de verdad de los objetos: editar precios/nombres/descripciones = editar
// esos info.json (o regenerarlos con scratchpad/export_catalog.py). La carpeta se copia al output del
// server (ver .csproj) para estar disponible en runtime (local y Render).
public static class CatalogLoader
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    public static List<ShopItem> Load(string catalogRoot)
    {
        var list = new List<ShopItem>();
        if (!Directory.Exists(catalogRoot)) return list;

        // Estructura: Catalog/<Categoría>/<Objeto>/info.json → se busca recursivamente.
        foreach (var f in Directory.EnumerateFiles(catalogRoot, "info.json", SearchOption.AllDirectories).OrderBy(p => p))
        {
            try
            {
                var info = JsonSerializer.Deserialize<CatalogInfo>(File.ReadAllText(f), Opts);
                if (info == null || string.IsNullOrWhiteSpace(info.Name)) continue;
                list.Add(new ShopItem
                {
                    Name = info.Name,
                    Icon = info.Icon ?? "",
                    Price = info.Price,
                    Description = info.Description ?? "",
                    StyleKey = info.StyleKey ?? "",
                    Category = info.Category ?? "",
                    SpriteId = info.SpriteId ?? "",
                    Currency = string.IsNullOrEmpty(info.Currency) ? "Gold" : info.Currency,
                    Rarity = string.IsNullOrEmpty(info.Rarity) ? "Common" : info.Rarity,
                    AvailableFrom = info.AvailableFrom,
                    AvailableTo = info.AvailableTo,
                    Source = info.Source ?? "",
                    Effect = info.Effect ?? "",
                    EffectValue = info.EffectValue,
                    GridW = info.Footprint is { Length: 2 } ? Math.Max(1, info.Footprint[0]) : 1,
                    GridD = info.Footprint is { Length: 2 } ? Math.Max(1, info.Footprint[1]) : 1,
                    Slot = info.Slot ?? "",
                });
            }
            catch { /* info.json inválido → se omite ese objeto */ }
        }
        return list;
    }

    private class CatalogInfo
    {
        public string Name { get; set; } = "";
        public string? Icon { get; set; }
        public int Price { get; set; }
        public string? Description { get; set; }
        public string? StyleKey { get; set; }
        public string? Category { get; set; }
        public string? SpriteId { get; set; }
        public string? Currency { get; set; }
        public string? Rarity { get; set; }
        public DateTime? AvailableFrom { get; set; }
        public DateTime? AvailableTo { get; set; }
        public string? Source { get; set; }
        public string? Effect { get; set; }
        public int EffectValue { get; set; }
        public int[]? Footprint { get; set; }   // [w,d] en celdas de la vista por defecto (_l); ausente = 1×1
        public string? Slot { get; set; }        // "wall" = va colgado en las paredes traseras; ausente = piso
    }
}
