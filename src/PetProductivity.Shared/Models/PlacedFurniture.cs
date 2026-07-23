namespace PetProductivity.Shared.Models;

// Un mueble colocado en el cuarto (F5.2). Autocontenido para renderizar sin recargar el catálogo:
// lleva el sprite ya resuelto (con su vista) y su footprint en celdas.
public class PlacedFurniture
{
    public string Name { get; set; } = string.Empty;   // nombre del ítem del catálogo (para validar propiedad)
    public string Sprite { get; set; } = string.Empty;  // clave de sprite lista para dibujar, ej "obj_bed_l"
    public int GridX { get; set; }
    public int GridY { get; set; }
    public int GridW { get; set; } = 1;
    public int GridD { get; set; } = 1;
    // true = colgado en un riel de pared (celda de borde trasero); no ocupa piso. JSON viejo → false.
    public bool OnWall { get; set; }
    // true = decoración de piso (alfombra): no bloquea ni es bloqueada por otros objetos, y se
    // dibuja siempre debajo. JSON viejo → false (se sigue tratando como mueble normal).
    public bool IsFloorDecor { get; set; }
}
