namespace PetProductivity.Client.Controls;

/// <summary>
/// Definición de un mueble que ocupa espacio en la grilla.
/// </summary>
public record FurnitureDef(
    string Id,
    int GridW,
    int GridD,
    string SpriteName
);

/// <summary>
/// Representa un mueble instanciado en una posición específica de la grilla.
/// </summary>
public record FurniturePlacement(
    FurnitureDef Def,
    int GridX, 
    int GridY
);

/// <summary>
/// Modelo lógico del cuarto. Maneja una grilla NxM y la ocupación de celdas por muebles.
/// </summary>
public class RoomGrid
{
    public int Width { get; }
    public int Depth { get; }
    
    private FurniturePlacement?[,] _cells;
    private List<FurniturePlacement> _placements = new();

    public IReadOnlyList<FurniturePlacement> Placements => _placements;

    public RoomGrid(int width, int depth)
    {
        Width = width;
        Depth = depth;
        _cells = new FurniturePlacement?[width, depth];
    }

    /// <summary>
    /// Intenta colocar un mueble en la coordenada indicada (x,y). 
    /// Retorna false si no cabe o choca con otro.
    /// </summary>
    public bool TryPlace(FurnitureDef def, int x, int y)
    {
        if (!IsAreaFree(x, y, def.GridW, def.GridD)) return false;

        var placement = new FurniturePlacement(def, x, y);
        _placements.Add(placement);
        
        for (int i = 0; i < def.GridW; i++)
        {
            for (int j = 0; j < def.GridD; j++)
            {
                _cells[x + i, y + j] = placement;
            }
        }
        
        return true;
    }

    /// <summary>
    /// Verifica si un área WxD está completamente libre (y dentro de los límites).
    /// </summary>
    public bool IsAreaFree(int x, int y, int w, int d)
    {
        if (x < 0 || y < 0 || x + w > Width || y + d > Depth) return false;
        
        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < d; j++)
            {
                if (_cells[x + i, y + j] != null) return false;
            }
        }
        return true;
    }

    public void Clear()
    {
        _placements.Clear();
        _cells = new FurniturePlacement?[Width, Depth];
    }
}
