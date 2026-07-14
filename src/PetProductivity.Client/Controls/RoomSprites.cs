using Microsoft.Maui.Storage;
using SkiaSharp;

namespace PetProductivity.Client.Controls;

// Carga y cachea (una vez) los sprites de la sala (pack Bongseng) desde Resources/Raw.
// Se cargan por nombre con FileSystem.OpenAppPackageFileAsync (MauiAsset, LogicalName = nombre de archivo).
// Si falta algún archivo, RoomDiorama hace fallback al dibujo procedural (nunca pantalla en blanco).
static class RoomSprites
{
    static readonly Dictionary<string, SKImage> _cache = new();
    static readonly HashSet<string> _missing = new(); // caché negativa: no reintentar en cada repintado
    static bool _loading;
    public static bool Ready { get; private set; }

    // Fondo del cuarto + muebles del seed (pack Bongseng, convención obj_<id>_<vista>).
    // El catálogo completo de la tienda se cargará bajo demanda en F5 (no precargar cientos aquí).
    static readonly string[] Names =
    {
        "room_bg",                          // fondo único del cuarto (piso+paredes) — ver ROOM_PIECE_SPEC.md
        "obj_bed_l", "obj_plant", "obj_lamp_l", "obj_cat_l",
    };

    public static SKImage? Get(string name) => _cache.TryGetValue(name, out var img) ? img : null;

    // Carga bajo demanda sprites arbitrarios (obj_* colocados por el usuario, F5.2) que no están en el seed.
    public static async Task EnsureNamedAsync(IEnumerable<string> names, Action? onReady = null)
    {
        bool any = false;
        foreach (var n in names)
        {
            if (string.IsNullOrEmpty(n) || _cache.ContainsKey(n) || _missing.Contains(n)) continue;
            try
            {
                using var s = await FileSystem.OpenAppPackageFileAsync($"{n}.png");
                using var ms = new MemoryStream();
                await s.CopyToAsync(ms);
                ms.Position = 0;
                using var bmp = SKBitmap.Decode(ms);
                if (bmp != null) { _cache[n] = SKImage.FromBitmap(bmp); any = true; }
                else _missing.Add(n);
            }
            catch { _missing.Add(n); /* ausente → no reintentar */ }
        }
        if (any) onReady?.Invoke();
    }

    public static async Task EnsureLoadedAsync(Action? onReady = null)
    {
        if (Ready) { onReady?.Invoke(); return; }
        if (_loading) return;
        _loading = true;
        try
        {
            foreach (var n in Names)
            {
                try
                {
                    using var s = await FileSystem.OpenAppPackageFileAsync($"{n}.png");
                    using var ms = new MemoryStream();
                    await s.CopyToAsync(ms);
                    ms.Position = 0;
                    using var bmp = SKBitmap.Decode(ms);
                    if (bmp != null) _cache[n] = SKImage.FromBitmap(bmp);
                }
                catch { /* archivo ausente → se omite; el control hace fallback */ }
            }
            
            // "Ready" cuando el fondo del cuarto cargó (los muebles se componen encima por grilla).
            Ready = _cache.ContainsKey("room_bg");
        }
        finally { _loading = false; }
        onReady?.Invoke();
    }
}
