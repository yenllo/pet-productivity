using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetProductivity.Server.Data;
using PetProductivity.Server.Services;
using PetProductivity.Shared.Models;

namespace PetProductivity.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ShopController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly PetWriteLock _petLock;
    private readonly IWebHostEnvironment _env;
    // Catálogo = ÚNICA fuente de verdad en la carpeta Catalog/ (una subcarpeta por objeto con su info.json).
    // Se carga una vez al arrancar. Para cambiar precios/nombres/objetos: editar los info.json (o
    // regenerarlos con scratchpad/export_catalog.py) y reiniciar el server.
    private static readonly List<ShopItem> ItemCatalog =
        CatalogLoader.Load(Path.Combine(AppContext.BaseDirectory, "Catalog"));

    // Solo ítems dentro de su ventana de disponibilidad (F5.3). null en fechas = siempre.
    static bool IsAvailable(ShopItem i, DateTime now) =>
        (i.AvailableFrom == null || now >= i.AvailableFrom) && (i.AvailableTo == null || now <= i.AvailableTo);

    [AllowAnonymous]
    [HttpGet("catalog")]
    public IActionResult GetCatalog()
    {
        var now = DateTime.UtcNow;
        return Ok(ItemCatalog.Where(i => IsAvailable(i, now)).ToList());
    }

    public ShopController(AppDbContext context, PetWriteLock petLock, IWebHostEnvironment env)
    {
        _context = context;
        _petLock = petLock;
        _env = env;
    }

    [HttpPost("buy")]
    public async Task<IActionResult> BuyItem([FromBody] BuyRequest request)
    {
        var user = await _context.Users
            .Include(u => u.UserPet)
            .FirstOrDefaultAsync(u => u.Id == User.GetUserId()); // userId del token, no del body

        if (user == null) return NotFound("Usuario no encontrado.");

        var pet = user.UserPet;
        if (pet == null) return NotFound("Mascota no encontrada.");

        var catalogItem = ItemCatalog.FirstOrDefault(i => i.Name.Equals(request.ItemName, StringComparison.OrdinalIgnoreCase));
        if (catalogItem == null)
            return BadRequest("Ítem no válido.");

        // Regla de oro del proyecto: el oro NUNCA compra objetos Premium (van por dinero real).
        if (catalogItem.Currency == "Premium")
            return BadRequest("Este objeto es Premium: no se compra con oro.");
        if (!IsAvailable(catalogItem, DateTime.UtcNow))
            return BadRequest("Este objeto ya no está disponible.");

        int actualPrice = catalogItem.Price;

        // Serializa el gasto de oro de ESTA mascota (misma clave que el premio) + valores frescos:
        // evita doble-gasto si entran dos compras a la vez.
        using var _ = await _petLock.AcquireAsync(pet.Id);
        await _context.Entry(user).ReloadAsync(); // oro + inventario frescos
        await _context.Entry(pet).ReloadAsync();

        if (pet.GoldCoins < actualPrice)
            return BadRequest("No tienes suficiente oro.");

        pet.GoldCoins -= actualPrice;

        user.Inventory ??= new Dictionary<string, int>();
        if (!user.Inventory.ContainsKey(request.ItemName))
            user.Inventory[request.ItemName] = 0;
        user.Inventory[request.ItemName]++;

        // Efectos declarativos del catálogo (T19-C): la decisión vive en el info.json del ítem,
        // no en su nombre. La evolución avanza SOLO con XP de tareas: el oro es cosmético.
        switch (catalogItem.Effect)
        {
            case "heal": pet.Heal(catalogItem.EffectValue); break;
        }

        await _context.SaveChangesAsync();

        return Ok(new { Success = true, GoldRemaining = pet.GoldCoins });
    }

    // Equipa un estilo de habitación ya poseído (cosmético). Valida la propiedad server-side.
    [HttpPost("equip")]
    public async Task<IActionResult> EquipStyle([FromBody] EquipStyleRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == User.GetUserId());
        if (user == null) return NotFound("Usuario no encontrado.");

        var key = (request.StyleKey ?? string.Empty).Trim();
        if (key.Length == 0 || key == "default")
        {
            user.ActiveRoomStyle = "default";
        }
        else
        {
            var styleItem = ItemCatalog.FirstOrDefault(i => i.StyleKey == key);
            if (styleItem == null) return BadRequest("Estilo no válido.");
            if (user.Inventory == null || !user.Inventory.ContainsKey(styleItem.Name))
                return BadRequest("No posees ese estilo.");
            user.ActiveRoomStyle = key;
        }

        await _context.SaveChangesAsync();
        return Ok(new { ActiveRoomStyle = user.ActiveRoomStyle });
    }

    // Guarda la disposición del cuarto (F5.2). Colocar es 100% cosmético (sin oro/XP): NO se valida contra
    // el inventario del usuario, solo sanidad de forma (sprite del pack + cabe en la grilla). La
    // grilla/colisión las maneja el cliente. Impacto de esto: un request forjado podría colocar un sprite
    // no comprado, pero solo en el CUARTO PROPIO del atacante — no mueve oro/XP ni afecta a otros.
    [HttpPost("placements")]
    public async Task<IActionResult> SavePlacements([FromBody] List<PlacedFurniture> placements)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == User.GetUserId());
        if (user == null) return NotFound("Usuario no encontrado.");

        placements ??= new();
        // Colocar es 100% cosmético (sin oro/XP), así que no gateamos por inventario: solo sanidad — el
        // sprite debe ser una pieza del pack (obj_*) y caber en la grilla lógica. Añadir muebles nuevos al
        // cuarto pasa siempre por comprar (auto-place); el editor solo reordena/quita lo ya colocado.
        var clean = placements
            .Where(p => !string.IsNullOrEmpty(p.Sprite) && p.Sprite.StartsWith("obj_")
                        && p.GridX >= 0 && p.GridY >= 0 && p.GridW >= 1 && p.GridD >= 1)
            .Take(64)
            .ToList();

        user.PlacedFurniture = clean;
        await _context.SaveChangesAsync();
        return Ok(new { Placed = clean.Count });
    }

    // Compra Premium con dinero real (F5.4). El pago se confirma con la tienda ANTES de otorgar: aquí se
    // valida el recibo SERVER-SIDE (obligatorio). En Development un stub acepta un recibo no vacío (sin claves
    // en el repo); en producción hay que conectar Google Play Billing (requiere la cuenta del dueño) → hoy
    // deshabilitado. El oro nunca entra aquí; lo premium es 100% cosmético.
    [HttpPost("purchase-premium")]
    public async Task<IActionResult> PurchasePremium([FromBody] PremiumPurchaseRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == User.GetUserId());
        if (user == null) return NotFound("Usuario no encontrado.");

        var item = ItemCatalog.FirstOrDefault(i => i.Name.Equals(request.ItemName, StringComparison.OrdinalIgnoreCase));
        if (item == null || item.Currency != "Premium") return BadRequest("Objeto premium no válido.");
        if (!IsAvailable(item, DateTime.UtcNow)) return BadRequest("Este objeto ya no está disponible.");

        // Validación del recibo (placeholder del proveedor real).
        bool receiptOk = _env.IsDevelopment()
            ? !string.IsNullOrWhiteSpace(request.Receipt)            // stub de prueba
            : false;                                                 // TODO: Google Play Billing (cuenta del dueño)
        if (!receiptOk)
            return BadRequest(_env.IsDevelopment() ? "Recibo vacío." : "Los pagos premium aún no están habilitados.");

        user.Inventory ??= new();
        user.Inventory[item.Name] = user.Inventory.GetValueOrDefault(item.Name) + 1;
        await _context.SaveChangesAsync();
        return Ok(new { Success = true });
    }
}

public class BuyRequest
{
    public string ItemName { get; set; } = string.Empty;
}

public class EquipStyleRequest
{
    public string StyleKey { get; set; } = string.Empty;
}

public class PremiumPurchaseRequest
{
    public string ItemName { get; set; } = string.Empty;
    // Recibo/purchase-token del proveedor (Google Play). En Development basta con que no esté vacío.
    public string Receipt { get; set; } = string.Empty;
}
