using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using PetProductivity.Shared.Models;

namespace PetProductivity.Client.Controls;

// Diorama isométrico VIVO (suelo continuo + ventana con cielo día/noche, nubes y haz de luz,
// planta y cortinas que se mecen, motas, brillo de lámpara). La mascota va como overlay encima
// en cada página (queda quieta salvo en Foco). Reutilizable: Dashboard, grupo y Foco.
// Un solo timer: la página engancha FrameTick para animar su overlay (respiración) sin abrir otro.
// La paleta (paredes/suelo/alfombra/cama) cambia con RoomStyle (estilo comprado en la tienda).
public class RoomDiorama : SKCanvasView
{
    public static readonly BindableProperty IsCrystallizedProperty =
        BindableProperty.Create(nameof(IsCrystallized), typeof(bool), typeof(RoomDiorama), false,
            propertyChanged: (b, _, _) => ((RoomDiorama)b).InvalidateSurface());

    public bool IsCrystallized
    {
        get => (bool)GetValue(IsCrystallizedProperty);
        set => SetValue(IsCrystallizedProperty, value);
    }

    public static readonly BindableProperty RoomStyleProperty =
        BindableProperty.Create(nameof(RoomStyle), typeof(string), typeof(RoomDiorama), "default",
            propertyChanged: (b, _, _) => ((RoomDiorama)b).InvalidateSurface());

    public string RoomStyle
    {
        get => (string)GetValue(RoomStyleProperty);
        set => SetValue(RoomStyleProperty, value);
    }

    public static readonly BindableProperty FurnitureProperty =
        BindableProperty.Create(nameof(Furniture), typeof(string), typeof(RoomDiorama), string.Empty,
            propertyChanged: (b, _, _) => ((RoomDiorama)b).InvalidateSurface());

    // CSV de claves de muebles poseídos (lamp/poster/clock); cada uno se dibuja en su slot fijo.
    public string Furniture
    {
        get => (string)GetValue(FurnitureProperty);
        set => SetValue(FurnitureProperty, value);
    }

    public static readonly BindableProperty RoomWidthProperty =
        BindableProperty.Create(nameof(RoomWidth), typeof(int), typeof(RoomDiorama), 6,
            propertyChanged: (b, _, _) => { ((RoomDiorama)b)._grid = null; ((RoomDiorama)b).InvalidateSurface(); });

    public int RoomWidth
    {
        get => (int)GetValue(RoomWidthProperty);
        set => SetValue(RoomWidthProperty, value);
    }

    public static readonly BindableProperty RoomDepthProperty =
        BindableProperty.Create(nameof(RoomDepth), typeof(int), typeof(RoomDiorama), 6,
            propertyChanged: (b, _, _) => { ((RoomDiorama)b)._grid = null; ((RoomDiorama)b).InvalidateSurface(); });

    public int RoomDepth
    {
        get => (int)GetValue(RoomDepthProperty);
        set => SetValue(RoomDepthProperty, value);
    }

    // Muebles colocados por el usuario (F5.2). Si es no-vacío reemplaza el seed por defecto.
    public static readonly BindableProperty PlacementsProperty =
        BindableProperty.Create(nameof(Placements), typeof(IReadOnlyList<PlacedFurniture>), typeof(RoomDiorama), null,
            propertyChanged: (b, _, _) => { var r = (RoomDiorama)b; r._grid = null; r.EnsurePlacementSprites(); r.InvalidateSurface(); });

    public IReadOnlyList<PlacedFurniture>? Placements
    {
        get => (IReadOnlyList<PlacedFurniture>?)GetValue(PlacementsProperty);
        set => SetValue(PlacementsProperty, value);
    }

    // Modo edición: dibuja la grilla del piso y resalta la celda/mueble seleccionado.
    public static readonly BindableProperty EditModeProperty =
        BindableProperty.Create(nameof(EditMode), typeof(bool), typeof(RoomDiorama), false,
            propertyChanged: (b, _, _) => ((RoomDiorama)b).InvalidateSurface());

    public bool EditMode
    {
        get => (bool)GetValue(EditModeProperty);
        set => SetValue(EditModeProperty, value);
    }

    // Celda resaltada en modo edición (mueble seleccionado). null = ninguna. Wall = objeto colgado
    // (piso y pared pueden compartir celda de borde, el flag desambigua a quién resaltar).
    public (int X, int Y, bool Wall)? Highlight { get; set; }

    // ---------- La mascota vive DENTRO del lienzo (antes era una <Image> de XAML encima) ----------
    // Como <Image>, Android la escalaba con filtro bilineal y el pixel-art salía borroso (los sprites
    // son 112x112 y se pintaban a 92x92: reescalado ni siquiera entero), mientras los muebles —
    // dibujados aquí con muestreo nearest— salían nítidos. Además iba centrada en la caja, así que
    // flotaba lejos de la sombra de contacto que este control le pinta en el suelo.
    // Aquí se dibuja con el mismo muestreo que los muebles y APOYADA en la baldosa central.
    public static readonly BindableProperty PetSpriteProperty =
        BindableProperty.Create(nameof(PetSprite), typeof(string), typeof(RoomDiorama), null,
            propertyChanged: (b, _, _) => { var r = (RoomDiorama)b; r.EnsurePetSprite(); r.InvalidateSurface(); });

    /// <summary>Nombre del archivo del sprite ("pet_sprout_baby.png"); se empaqueta también como MauiAsset.</summary>
    public string? PetSprite
    {
        get => (string?)GetValue(PetSpriteProperty);
        set => SetValue(PetSpriteProperty, value);
    }

    public static readonly BindableProperty PetOpacityProperty =
        BindableProperty.Create(nameof(PetOpacity), typeof(double), typeof(RoomDiorama), 1.0,
            propertyChanged: (b, _, _) => ((RoomDiorama)b).InvalidateSurface());

    public double PetOpacity
    {
        get => (double)GetValue(PetOpacityProperty);
        set => SetValue(PetOpacityProperty, value);
    }

    // Tamaño de la mascota en unidades del lienzo de diseño (1024²) — ~2,2 baldosas de ancho.
    const float PET_DESIGN = 330f;
    static readonly SKPaint PetPaint = new() { IsAntialias = false };
    float _celebrateUntil = -1;

    /// <summary>T5-D: saltitos de celebración al ganar XP (los dispara el Dashboard).</summary>
    public void Celebrate() => _celebrateUntil = _t + 1.2f;

    // Rechazo de movimiento en modo edición: rombo rojo fugaz sobre la celda destino (complementa el Toast).
    (int X, int Y, int W, int D) _invalidRect;
    float _invalidUntil = -1;
    public void FlashInvalid(int x, int y, int w, int d)
    { _invalidRect = (x, y, w, d); _invalidUntil = _t + 0.4f; InvalidateSurface(); }

    void EnsurePetSprite()
    {
        var name = Path.GetFileNameWithoutExtension(PetSprite);
        if (string.IsNullOrEmpty(name) || RoomSprites.Get(name) != null) return;
        _ = RoomSprites.EnsureNamedAsync(new[] { name }, () => MainThread.BeginInvokeOnMainThread(InvalidateSurface));
    }

    private RoomGrid? _grid;

    // Orden de dibujo (back-to-front) cacheado: depende solo de las posiciones, que no cambian entre
    // frames. Se ordenaba 25 veces por segundo para obtener siempre lo mismo. Ojo: RoomGrid muta su
    // lista EN SITIO al colocar un mueble, así que la referencia de la grilla no basta para invalidar
    // — por eso RoomGrid.Version.
    private List<FurniturePlacement>? _sorted;
    private RoomGrid? _sortedFor;
    private int _sortedVersion = -1;

    private List<FurniturePlacement> SortedPlacements()
    {
        if (_sorted == null || !ReferenceEquals(_sortedFor, _grid) || _sortedVersion != _grid!.Version)
        {
            _sorted = _grid!.Placements.OrderBy(p => p.GridX + p.GridY).ToList();
            _sortedFor = _grid;
            _sortedVersion = _grid.Version;
        }
        return _sorted;
    }

    // Carga los sprites que referencian las colocaciones (además del seed base) y repinta al terminar.
    void EnsurePlacementSprites()
    {
        var keys = Placements?.Select(p => p.Sprite).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToArray();
        if (keys == null || keys.Length == 0) return;
        _ = RoomSprites.EnsureNamedAsync(keys, () => MainThread.BeginInvokeOnMainThread(InvalidateSurface));
    }

    // Contrato de tamaño (decisión del dueño 2026-07-22): el ancho dibujado ES el ancho iso del
    // footprint — sin factores por objeto. Antes había mods calibrados a ojo (cama 1.6, closet 1.5…)
    // que compensaban footprints 1×1 mal declarados: la cama se dibujaba 36% más ancha que sus celdas
    // y se trepaba al muro. El tamaño ahora se corrige en los DATOS (footprint del catálogo) y el
    // arte futuro se autorea asumiendo mod 1 (un objeto sub-celda trae su margen en el propio sprite).

    void EnsureGrid()
    {
        if (_grid != null && _grid.Width == RoomWidth && _grid.Depth == RoomDepth) return;
        
        _grid = new RoomGrid(RoomWidth, RoomDepth);

        // Si el usuario ya colocó muebles (F5.2), esos mandan; el sprite ya trae la vista resuelta.
        var placed = Placements;
        if (placed != null && placed.Count > 0)
        {
            // Los colgados (OnWall) NO entran a la grilla de piso: si ocuparan la celda, el mueble de piso
            // legítimo en esa misma celda dejaría de dibujarse (TryPlace falla en silencio).
            foreach (var p in placed.Where(p => !p.OnWall))
                _grid.TryPlace(new FurnitureDef(p.Sprite, Math.Max(1, p.GridW), Math.Max(1, p.GridD), p.Sprite), p.GridX, p.GridY);
            return;
        }

        // Seed inicial (slots fijos, calibrado por simulación). Centro (3,3) queda libre para la mascota.
        // Sprites = pack Bongseng (obj_<id>_<vista>). Regla de vista de muebles de 2 vistas (cerrada):
        // contra pared derecha → "l", contra pared izquierda → "r". Como los slots son fijos, la vista se
        // resuelve aquí. ponytail: el modelo ViewSet/Slot llega con el modo edición de la tienda (F5.2);
        // hoy no hay rotación que justifique la abstracción.
        var furn = Furniture ?? string.Empty;

        // Cama: contra la pared trasera-derecha → vista "l".
        _grid.TryPlace(new FurnitureDef("bed", 2, 2, "obj_bed_l"), RoomWidth - 3, 1);
        // Lámpara de pie: esquina trasera-izquierda (si se posee).
        if (furn.Contains("lamp"))
            _grid.TryPlace(new FurnitureDef("lamp", 1, 1, "obj_lamp_l"), 0, 0);
        // Planta: esquina frontal-izquierda (simétrica, sin vista).
        _grid.TryPlace(new FurnitureDef("plant", 1, 1, "obj_plant"), 0, RoomDepth - 2);
        // Gato: frontal-derecha, fuera de la columna de la mascota.
        _grid.TryPlace(new FurnitureDef("cat", 1, 1, "obj_cat_l"), RoomWidth - 1, RoomDepth / 2);
    }

    // La página engancha esto para mover su overlay (respiración) con el mismo reloj.
    public event Action<float>? FrameTick;

    // Modo edición: la página se entera de qué celda de piso tocó el usuario (grilla lógica).
    public event Action<int, int>? CellTapped;

    // Arrastre directo (imán a celda): Pressed sobre el lienzo → Started; Moved solo al CRUZAR de
    // celda (no por pixel); Released tras arrastrar → Ended (y NO CellTapped: sería toggle-deselect).
    public event Action<int, int>? DragStarted;
    public event Action<int, int>? DragMoved;
    public event Action? DragEnded;

    // Geometría del último frame (para invertir pantalla→grilla al tocar).
    float _lastScale = 1, _lastOffX, _lastOffY;

    public RoomDiorama()
    {
        InitParticles();
        // Cargar sprites de la sala (Bongseng) en segundo plano; al terminar, repintar.
        _ = RoomSprites.EnsureLoadedAsync(() => MainThread.BeginInvokeOnMainThread(InvalidateSurface));
        EnableTouchEvents = true;
        Touch += OnTouch;
    }

    (int X, int Y)? _dragCell;   // última celda notificada durante el gesto actual
    bool _dragMoved;             // ¿el dedo cruzó a otra celda desde el Pressed?

    void OnTouch(object? sender, SKTouchEventArgs e)
    {
        if (EditMode)
            switch (e.ActionType)
            {
                case SKTouchAction.Pressed when TryScreenToGrid(e.Location, out int px, out int py):
                    _dragCell = (px, py);
                    _dragMoved = false;
                    DragStarted?.Invoke(px, py);
                    break;
                case SKTouchAction.Moved when _dragCell != null && TryScreenToGrid(e.Location, out int mx, out int my):
                    if (_dragCell != (mx, my))
                    {
                        _dragCell = (mx, my);
                        _dragMoved = true;
                        DragMoved?.Invoke(mx, my);
                    }
                    break;
                case SKTouchAction.Released or SKTouchAction.Cancelled or SKTouchAction.Exited:
                    bool wasDrag = _dragMoved;
                    _dragCell = null;
                    _dragMoved = false;
                    DragEnded?.Invoke();
                    // Tap = soltar sin haber salido de la celda inicial (el flujo actual intacto).
                    if (!wasDrag && e.ActionType == SKTouchAction.Released && TryScreenToGrid(e.Location, out int gx, out int gy))
                        CellTapped?.Invoke(gx, gy);
                    break;
            }
        e.Handled = true;
    }

    // Invierte el mapeo iso: punto de pantalla (px del canvas) → celda de piso (i,j). false si cae fuera.
    bool TryScreenToGrid(SKPoint p, out int gx, out int gy)
    {
        gx = gy = 0;
        if (_lastScale <= 0) return false;
        float u = (p.X - _lastOffX) / _lastScale - O_X; // = (i-j)*T_W/2
        float v = (p.Y - _lastOffY) / _lastScale - O_Y; // = (i+j)*T_H/2
        float iMinusJ = u * 2f / T_W, iPlusJ = v * 2f / T_H;
        gx = (int)Math.Floor((iPlusJ + iMinusJ) / 2f);
        gy = (int)Math.Floor((iPlusJ - iMinusJ) / 2f);
        return gx >= 0 && gy >= 0 && gx < RoomWidth && gy < RoomDepth;
    }

    // Dibuja el rombo del piso celda por celda (guía clara) en modo edición.
    void DrawEditGrid(SKCanvas canvas, Func<float, float, SKPoint> iso)
    {
        using var line = new SKPaint { IsAntialias = true, Color = new SKColor(255, 255, 255, 80), StrokeWidth = Math.Max(1f, 1.6f * _lastScale), Style = SKPaintStyle.Stroke };
        for (int i = 0; i <= RoomWidth; i++) canvas.DrawLine(iso(i, 0), iso(i, RoomDepth), line);
        for (int j = 0; j <= RoomDepth; j++) canvas.DrawLine(iso(0, j), iso(RoomWidth, j), line);
        // Celda (3,3) reservada a la mascota: tinte rosado para que se entienda por qué bloquea.
        using var pet = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0x5F, 0x8F, 60) };
        using var petPath = Quad(iso(3, 3), iso(4, 3), iso(4, 4), iso(3, 4));
        canvas.DrawPath(petPath, pet);
    }

    static readonly SKSamplingOptions RoomSampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);
    // Muebles = pixel-art Bongseng escalado ~2x; nearest mantiene el borde nítido (linear los emborrona).
    static readonly SKSamplingOptions PixelSampling = new(SKFilterMode.Nearest, SKMipmapMode.None);
    // Objeto seleccionado en modo edición: semitransparente para ver la grilla/huella debajo.
    static readonly SKPaint GhostPaint = new() { Color = SKColors.White.WithAlpha(150) };

    // ---------- Bucle de animación (un solo timer mueve todo el escenario) ----------
    // ponytail: ~25fps con blur ligero; si pesa en gama baja, subir intervalo o quitar partículas.
    // (Se omite cachear capas estáticas en SKPicture: no hay problema de rendimiento medido.)
    IDispatcherTimer? _timer;
    float _t;

    public void StartAnimation()
    {
        if (_timer != null) { _timer.Start(); return; }
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(40);
        _timer.Tick += (_, _) =>
        {
            _t += 0.04f;
            FrameTick?.Invoke(_t);
            InvalidateSurface();
        };
        _timer.Start();
    }

    public void StopAnimation() => _timer?.Stop();

    // ---------- Partículas (motas de luz) ----------
    struct Particle { public float X, Y, Speed, Size, Phase; }
    Particle[] _particles = Array.Empty<Particle>();

    void InitParticles()
    {
        var r = new Random(7);
        _particles = new Particle[16];
        for (int i = 0; i < _particles.Length; i++)
            _particles[i] = new Particle
            {
                X = (float)r.NextDouble(),
                Y = (float)r.NextDouble(),
                Speed = 0.02f + (float)r.NextDouble() * 0.05f,
                Size = 1.5f + (float)r.NextDouble() * 3f,
                Phase = (float)r.NextDouble() * 6.28f
            };
    }

    const int N = 5; // 5x5 casillas (geometría iso)

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);
        PaintRoom(e.Info, e.Surface.Canvas);
    }

    void PaintRoom(SKImageInfo info, SKCanvas canvas)
    {
        if (RoomSprites.Ready) { PaintModularRoom(canvas, info); return; }
        canvas.Clear();

        float W = info.Width, H = info.Height;
        float tileW = W / 6.2f;
        float tileH = tileW / 2f;
        float wallH = tileW * 0.62f;
        float floorH = N * tileH;
        float originX = W / 2f;
        float originY = (H - (floorH + wallH)) / 2f + wallH;
        if (originY < wallH + 6) originY = wallH + 6;

        SKPoint Iso(float i, float j) => new(originX + (i - j) * (tileW / 2f), originY + (i + j) * (tileH / 2f));

        var sky = DayNight();
        var pal = Palette(RoomStyle);
        var furn = Furniture ?? string.Empty; // claves de muebles poseídos: lamp/poster/clock

        // Luz ambiental desde arriba-derecha — más cálida al atardecer, tenue de noche
        byte ambA = (byte)Math.Clamp(18 + 46 * sky.Light + 40 * sky.Warm, 0, 255);
        var ambColor = LerpC(new SKColor(255, 240, 220), new SKColor(255, 180, 120), sky.Warm);
        using (var amb = new SKPaint { IsAntialias = true, Shader = SKShader.CreateRadialGradient(
                new SKPoint(W * 0.82f, H * 0.04f), W * 0.95f,
                new[] { ambColor.WithAlpha(ambA), ambColor.WithAlpha(0) },
                new[] { 0f, 1f }, SKShaderTileMode.Clamp) })
            canvas.DrawRect(0, 0, W, H, amb);

        var A = Iso(0, 0); var L = Iso(0, N); var R = Iso(N, 0); var B = Iso(N, N);

        // Oclusión difuminada donde paredes encuentran el suelo
        SoftShadow(canvas, A.X, A.Y - wallH * 0.2f, tileW * 2.4f, wallH * 0.9f, 70, wallH * 0.5f);

        // Paredes (atenuadas de noche)
        float wb = 0.5f + 0.5f * sky.Light;
        DrawWall(canvas, A, L, wallH, Scale(pal.WallLTop, wb), Scale(pal.WallLBot, wb));
        DrawWall(canvas, A, R, wallH, Scale(pal.WallRTop, wb), Scale(pal.WallRBot, wb));

        // Ventana (pared derecha): cielo día/noche + nubes a la deriva + marco + cortinas
        DrawWindow(canvas, Iso(2f, 0), Iso(3.4f, 0), wallH, sky);

        // Muebles de pared comprables (solo si se poseen)
        if (furn.Contains("poster")) DrawPoster(canvas, Iso(0, 1.1f), Iso(0, 2.2f), wallH);
        if (furn.Contains("clock")) DrawClock(canvas, Iso(4.0f, 0), wallH);

        // --- Suelo CONTINUO (tablones a lo largo de un eje → lee como piso, no como grilla) ---
        for (int i = 0; i < N; i++)
        {
            int d = (i % 2 == 0) ? 0 : 8; // variación MUY sutil entre tablones
            using var plank = new SKPaint { IsAntialias = true, Color = Scale(Offset(pal.Floor, d), 0.7f + 0.3f * sky.Light) };
            canvas.DrawPath(Quad(Iso(i, 0), Iso(i + 1, 0), Iso(i + 1, N), Iso(i, N)), plank);
        }
        // Costuras tenues SOLO en una dirección (no es rejilla)
        using (var seam = new SKPaint { IsAntialias = true, Color = new SKColor(0, 0, 0, 30), StrokeWidth = 1, Style = SKPaintStyle.Stroke })
            for (int i = 1; i < N; i++) canvas.DrawLine(Iso(i, 0), Iso(i, N), seam);

        // Realce cálido hacia el centro del suelo
        using (var floorLight = new SKPaint { IsAntialias = true, Shader = SKShader.CreateRadialGradient(
                new SKPoint(originX + tileW, originY + floorH * 0.5f), tileW * 2.6f,
                new[] { new SKColor(255, 245, 230, (byte)(16 + 22 * sky.Light)), new SKColor(255, 245, 230, 0) },
                new[] { 0f, 1f }, SKShaderTileMode.Clamp) })
            canvas.DrawPath(Quad(A, R, B, L), floorLight);

        // Haz de luz de la ventana sobre el suelo (sutil, respira; más fuerte de día)
        float beam = 0.5f + 0.5f * (float)Math.Sin(_t * 0.6f);
        using (var shaft = new SKPaint { IsAntialias = true, Color = new SKColor(255, 240, 200,
                (byte)((22 + 16 * beam) * (0.3f + 0.7f * sky.Light))) })
            canvas.DrawPath(Quad(Iso(2f, 0.2f), Iso(3.4f, 0.2f), Iso(2.6f, 2.6f), Iso(1.2f, 2.6f)), shaft);

        var center = Iso(N / 2f, N / 2f);

        // Planta en una esquina (las hojas se mecen)
        DrawPlant(canvas, Iso(0.55f, 1.7f), tileW);

        // Lámpara de pie comprable (mueble de suelo, brilla de noche)
        if (furn.Contains("lamp")) DrawLamp(canvas, Iso(0.7f, 3.7f), tileW, sky);

        // Aura/bloom que late bajo la mascota
        float pulse = 0.5f + 0.5f * (float)Math.Sin(_t * 2 * Math.PI / 2.6);
        byte auraA = (byte)(70 + 60 * pulse);
        float auraR = tileW * (1.5f + 0.18f * pulse);
        using (var aura = new SKPaint { IsAntialias = true, Shader = SKShader.CreateRadialGradient(
                new SKPoint(center.X, center.Y - tileH * 0.4f), auraR,
                new[] { new SKColor(0x3D, 0xDC, 0x97, auraA), new SKColor(0x3D, 0xDC, 0x97, 0) },
                new[] { 0f, 1f }, SKShaderTileMode.Clamp) })
            canvas.DrawCircle(center.X, center.Y - tileH * 0.4f, auraR, aura);

        // Alfombra
        SoftShadow(canvas, Iso(2, 3.5f).X, Iso(2, 3.5f).Y + 4, tileW * 0.7f, tileH * 0.6f, 60, 10);
        using (var rug = new SKPaint { IsAntialias = true, Color = pal.Rug.WithAlpha(150) })
            canvas.DrawPath(Quad(Iso(1, 3), Iso(3, 3), Iso(3, 4), Iso(1, 4)), rug);

        // Cama (caja iso)
        var bedBase = Iso(4.5f, 1.5f);
        SoftShadow(canvas, bedBase.X, bedBase.Y + 4, tileW * 0.55f, tileH * 0.6f, 80, 9);
        DrawBox(canvas, Iso(4, 1), Iso(5, 1), Iso(5, 2), Iso(4, 2), tileW * 0.34f,
            pal.BedTop, pal.BedSide, pal.BedSideDark);

        // Sombra de contacto de la mascota (bajo el overlay)
        SoftShadow(canvas, center.X, center.Y + tileH * 0.15f, tileW * 0.5f, tileH * 0.5f, 95, 8);

        // Brillo de lámpara cálido (cozy) — fuerte de noche, nulo de día
        if (sky.Light < 0.85f)
        {
            var lampP = Iso(4.6f, 1.4f);
            float ly = lampP.Y - wallH * 0.2f;
            using var lamp = new SKPaint { IsAntialias = true, Shader = SKShader.CreateRadialGradient(
                new SKPoint(lampP.X, ly), tileW * 1.6f,
                new[] { new SKColor(255, 210, 140, (byte)(70 * (1 - sky.Light))), new SKColor(255, 210, 140, 0) },
                new[] { 0f, 1f }, SKShaderTileMode.Clamp) };
            canvas.DrawCircle(lampP.X, ly, tileW * 1.6f, lamp);
        }

        // Partículas flotantes (motas de luz difuminadas)
        using (var dust = new SKPaint { IsAntialias = true, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2.2f) })
        {
            foreach (var p in _particles)
            {
                float y = (p.Y - p.Speed * _t) % 1f; if (y < 0) y += 1f;
                float x = p.X + 0.02f * (float)Math.Sin(_t + p.Phase);
                float px = x * W;
                float py = originY - wallH + y * (floorH + wallH);
                byte a = (byte)(40 + 60 * (0.5 + 0.5 * Math.Sin(_t * 1.5 + p.Phase)));
                dust.Color = new SKColor(255, 250, 235, a);
                canvas.DrawCircle(px, py, p.Size, dust);
            }
        }

        // Escarcha de cristalización
        if (IsCrystallized)
            using (var frost = new SKPaint { IsAntialias = true, Color = new SKColor(0x7F, 0xB0, 0xFF, 46) })
                canvas.DrawRect(0, 0, W, H, frost);
    }

    // ---------- Cuarto = fondo único (room_bg) + muebles por grilla (ROOM_PIECE_SPEC.md) ----------
    // Diseño del lienzo de la pieza: 1024x1024, iso 2:1, 6x6 tiles, vértice trasero en (512,330).
    const float DESIGN = 1024f, T_W = 150f, T_H = 75f, O_X = 512f, O_Y = 330f;
    // Altura del muro en px de diseño (ROOM_PIECE_SPEC). Los colgados se centran verticalmente en él:
    // así un sprite alto no sobresale por encima de la pared y uno bajito queda a media pared.
    const float WALL_H = 300f;

    void PaintModularRoom(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear();
        // Fondo por estilo equipado (room_bg_<styleKey>.png, misma geometría — ver ROOM_PIECE_SPEC.md);
        // si el arte del estilo no existe (aún) se usa el fondo base.
        SKImage? bg = null;
        if (!string.IsNullOrEmpty(RoomStyle) && RoomStyle != "default")
        {
            var styled = $"room_bg_{RoomStyle}";
            bg = RoomSprites.Get(styled);
            if (bg == null)
                _ = RoomSprites.EnsureNamedAsync(new[] { styled }, () => MainThread.BeginInvokeOnMainThread(InvalidateSurface));
        }
        bg ??= RoomSprites.Get("room_bg");
        if (bg == null) return; // sin fondo aún → no pintar (el gate de Ready ya lo exige)
        EnsureGrid();

        float W = info.Width, H = info.Height;
        var sky = DayNight();

        // Fit-contain centrado del lienzo 1024² en el canvas.
        float scale = Math.Min(W / DESIGN, H / DESIGN);
        float offX = (W - DESIGN * scale) / 2f;
        float offY = (H - DESIGN * scale) / 2f;
        // Punto de pantalla del nodo de grilla (i,j) en coords iso de diseño.
        SKPoint Iso(float i, float j) => new(
            offX + (O_X + (i - j) * T_W / 2f) * scale,
            offY + (O_Y + (i + j) * T_H / 2f) * scale);

        // 1) Fondo del cuarto (piso + paredes).
        canvas.DrawImage(bg, new SKRect(offX, offY, offX + DESIGN * scale, offY + DESIGN * scale), RoomSampling);

        // Geometría de este frame (para hit-testing pantalla→grilla en modo edición).
        _lastScale = scale; _lastOffX = offX; _lastOffY = offY;

        // Modo edición: grilla del piso bajo los muebles.
        if (EditMode) DrawEditGrid(canvas, Iso);
        if (EditMode && _t < _invalidUntil)
        {
            using var bad = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0x5A, 0x5A, 90) };
            using var badPath = Quad(Iso(_invalidRect.X, _invalidRect.Y), Iso(_invalidRect.X + _invalidRect.W, _invalidRect.Y),
                                     Iso(_invalidRect.X + _invalidRect.W, _invalidRect.Y + _invalidRect.D), Iso(_invalidRect.X, _invalidRect.Y + _invalidRect.D));
            canvas.DrawPath(badPath, bad);
        }

        // 1.5) Objetos colgados (OnWall): en las 2 paredes traseras, siempre detrás de todo el piso.
        // No pasan por RoomGrid (no ocupan celdas): se dibujan directo de Placements, anclados al centro
        // del borde trasero de su celda de riel y elevados WALL_LIFT sobre la base del muro.
        if (Placements is { } pls)
            foreach (var wp in pls)
            {
                if (!wp.OnWall) continue;
                var ws = RoomSprites.Get(wp.Sprite);
                if (ws == null) continue;
                bool right = wp.GridY == 0; // riel derecho = celdas (i,0); la esquina (0,0) es suya
                var bp = right ? Iso(wp.GridX + 0.5f, 0f) : Iso(0f, wp.GridY + 0.5f);
                float wW = T_W * scale; // colgados: 1 celda de riel de ancho (mod 1)
                float wH = wW * ws.Height / ws.Width;
                float lift = Math.Max(0f, (WALL_H * scale - wH) / 2f); // centrado vertical en el muro
                float bottom = bp.Y - lift;
                bool wallSel = EditMode && Highlight is { Wall: true } wh && wh.X == wp.GridX && wh.Y == wp.GridY;
                if (wallSel)
                {
                    // Rombo en su celda de riel: la referencia que mueven el pad y el tap.
                    using var fp = new SKPaint { IsAntialias = true, Color = new SKColor(0x3D, 0xDC, 0x97, 88) };
                    using var fpPath = Quad(Iso(wp.GridX, wp.GridY), Iso(wp.GridX + 1, wp.GridY),
                                            Iso(wp.GridX + 1, wp.GridY + 1), Iso(wp.GridX, wp.GridY + 1));
                    canvas.DrawPath(fpPath, fp);
                }
                canvas.DrawImage(ws, new SKRect(bp.X - wW / 2f, bottom - wH, bp.X + wW / 2f, bottom),
                                 PixelSampling, wallSel ? GhostPaint : null);
            }

        // 2) Muebles (back-to-front), anclados por borde inferior-centro al vértice frontal de su footprint.
        // El orden de dibujo depende solo de las posiciones, que no cambian entre frames: se ordena
        // una vez por cambio de grilla, no 25 veces por segundo.
        // La MASCOTA entra en este mismo orden por profundidad (está en la baldosa 3,3): así un mueble
        // que esté delante de ella la tapa, en vez de que ella quede siempre encima de todo.
        bool petDrawn = false;
        const int PET_DEPTH = 3 + 3; // clave de profundidad de la baldosa (3,3)

        if (_grid != null)
            foreach (var pl in SortedPlacements())
            {
                if (!petDrawn && pl.GridX + pl.GridY > PET_DEPTH) { DrawPet(); petDrawn = true; }

                var sprite = RoomSprites.Get(pl.Def.SpriteName);
                if (sprite == null) continue;
                // Ancla: X centrada en el footprint, base en el vértice FRONTAL del rombo — así la huella
                // queda debajo del mueble en vez de asomar por delante de sus pies.
                var centerPt = Iso(pl.GridX + pl.Def.GridW / 2f, pl.GridY + pl.Def.GridD / 2f);
                float frontY = Iso(pl.GridX + pl.Def.GridW, pl.GridY + pl.Def.GridD).Y;
                // Ancho en pantalla = ancho exacto del rombo iso W×D: (W+D)/2 · T_W (el sprite llena su huella).
                float wScr = T_W * (pl.Def.GridW + pl.Def.GridD) / 2f * scale;
                float hScr = wScr * sprite.Height / sprite.Width;
                if (pl.Def.GridW == 1 && pl.Def.GridD == 1)
                    SoftShadow(canvas, centerPt.X, centerPt.Y, wScr * 0.30f, hScr * 0.06f, 70, 6);
                bool selected = EditMode && Highlight is { Wall: false } hi && hi.X == pl.GridX && hi.Y == pl.GridY;
                if (selected)
                {
                    // Footprint WxD completo en el piso: ESTA es la huella real que ocupa el mueble.
                    using var fp = new SKPaint { IsAntialias = true, Color = new SKColor(0x3D, 0xDC, 0x97, 88) };
                    using var fpPath = Quad(Iso(pl.GridX, pl.GridY), Iso(pl.GridX + pl.Def.GridW, pl.GridY),
                                            Iso(pl.GridX + pl.Def.GridW, pl.GridY + pl.Def.GridD), Iso(pl.GridX, pl.GridY + pl.Def.GridD));
                    canvas.DrawPath(fpPath, fp);
                }
                canvas.DrawImage(sprite, new SKRect(centerPt.X - wScr / 2f, frontY - hScr, centerPt.X + wScr / 2f, frontY),
                                 PixelSampling, selected ? GhostPaint : null);
            }

        // 3) Si no había ningún mueble por delante, la mascota va aquí (encima de todo lo de atrás).
        if (!petDrawn) DrawPet();

        // Mascota: sombra de contacto + criatura APOYADA sobre ella en la baldosa central (3,3).
        void DrawPet()
        {
            var petPt = Iso(3f, 3f);
            var petImg = RoomSprites.Get(Path.GetFileNameWithoutExtension(PetSprite) ?? "");

            // Al saltar, la sombra encoge un pelo: es lo que vende el contacto con el suelo.
            float breathe = 1f + 0.03f * (float)Math.Sin(_t * 2 * Math.PI / 3.2);
            float hop = 0f;
            if (_t < _celebrateUntil)
            {
                float p = (_celebrateUntil - _t) / 1.2f;                 // 1 → 0
                hop = (float)(Math.Abs(Math.Sin((1 - p) * Math.PI * 3)) * 18 * p * scale);
            }
            float shrink = 1f - 0.25f * Math.Min(1f, hop / (18f * scale + 0.001f));
            SoftShadow(canvas, petPt.X, petPt.Y, W * 0.10f * shrink, H * 0.03f * shrink, 80, 8);

            if (petImg == null) return;
            // Ancla: borde INFERIOR-centro en la baldosa (los pies tocan el suelo, no el centro de la caja).
            float pw = PET_DESIGN * scale * breathe;
            float ph = pw * petImg.Height / petImg.Width;
            PetPaint.Color = new SKColor(255, 255, 255, (byte)(255 * Math.Clamp(PetOpacity, 0, 1)));
            canvas.DrawImage(petImg,
                new SKRect(petPt.X - pw / 2f, petPt.Y - ph - hop, petPt.X + pw / 2f, petPt.Y - hop),
                PixelSampling, PetPaint); // nearest: pixel-art nítido, igual que los muebles
        }

        // 4) Tinte día/noche. (Paint reutilizado: se pintaba 25 veces por segundo creando uno nuevo cada vez.)
        // Los alphas eran 150/60: al atardecer (warm≈1 justo a las 18:00) eso pintaba ~126 de naranja
        // + 60 encima = la sala quedaba AHOGADA en sepia y el arte no se leía (paredes crema→tan,
        // gato negro→marrón). Verificado en emulador comparando 17:49 vs 12:52 con el mismo arte.
        // Ambiente sí, repintar el arte no: bajados a 70/26. Sube TINT_MAX si quieres más drama nocturno.
        const float TINT_MAX = 70f, WARM_MAX = 26f;
        byte tintA = (byte)(TINT_MAX * (1 - sky.Light));
        if (tintA > 0)
        {
            FlatPaint.Color = sky.Top.WithAlpha(tintA);
            canvas.DrawRect(0, 0, W, H, FlatPaint);
        }
        if (sky.Warm > 0.05f)
        {
            FlatPaint.Color = new SKColor(255, 170, 90, (byte)(WARM_MAX * sky.Warm));
            canvas.DrawRect(0, 0, W, H, FlatPaint);
        }

        // 5) Motas flotantes.
        DustPaint.MaskFilter = Blur(2.2f);
        foreach (var p in _particles)
        {
            float y = (p.Y - p.Speed * _t) % 1f; if (y < 0) y += 1f;
            float x = p.X + 0.02f * (float)Math.Sin(_t + p.Phase);
            byte a = (byte)(40 + 60 * (0.5 + 0.5 * Math.Sin(_t * 1.5 + p.Phase)));
            DustPaint.Color = new SKColor(255, 250, 235, a);
            canvas.DrawCircle(x * W, y * H, p.Size, DustPaint);
        }

        if (IsCrystallized)
            using (var frost = new SKPaint { Color = new SKColor(0x7F, 0xB0, 0xFF, 46) })
                canvas.DrawRect(0, 0, W, H, frost);
    }

    // ---------- Ventana: cielo, nubes, estrellas, marco y cortinas ----------
    void DrawWindow(SKCanvas canvas, SKPoint baseL, SKPoint baseR, float wallH, Sky sky)
    {
        const float top = 0.80f, bot = 0.30f;
        var p0 = new SKPoint(baseL.X, baseL.Y - wallH * top); // sup-izq
        var p1 = new SKPoint(baseR.X, baseR.Y - wallH * top); // sup-der
        var p2 = new SKPoint(baseR.X, baseR.Y - wallH * bot); // inf-der
        var p3 = new SKPoint(baseL.X, baseL.Y - wallH * bot); // inf-izq
        var win = Quad(p0, p1, p2, p3);

        canvas.Save();
        canvas.ClipPath(win, SKClipOperation.Intersect, true);

        using (var skyP = new SKPaint { IsAntialias = true, Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, p0.Y), new SKPoint(0, p2.Y),
                new[] { sky.Top, sky.Bottom }, null, SKShaderTileMode.Clamp) })
            canvas.DrawPath(win, skyP);

        float wMin = Math.Min(p0.X, p3.X), wMax = Math.Max(p1.X, p2.X);
        float span = wMax - wMin, cy0 = (p0.Y + p2.Y) / 2f;

        // Nubes a la deriva (más visibles de día)
        byte cloudA = (byte)(120 * (0.25f + 0.75f * sky.Light));
        using (var cloud = new SKPaint { IsAntialias = true, Color = new SKColor(255, 255, 255, cloudA), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6f) })
            for (int k = 0; k < 3; k++)
            {
                float prog = (0.33f * k + _t * 0.012f * (1 + 0.3f * k)) % 1.2f;
                float cx = wMin + (prog - 0.1f) * span;
                float cy = cy0 - (k - 1) * wallH * 0.12f;
                canvas.DrawOval(new SKRect(cx - span * 0.22f, cy - wallH * 0.06f, cx + span * 0.22f, cy + wallH * 0.06f), cloud);
            }

        // Estrellas titilando (solo de noche)
        if (sky.Light < 0.25f)
            using (var star = new SKPaint { IsAntialias = true, Color = SKColors.White })
                for (int s = 0; s < 6; s++)
                {
                    float sx = wMin + span * (0.12f + 0.15f * s);
                    float sy = p0.Y + wallH * (0.12f + 0.06f * ((s * 7) % 5));
                    float tw = 0.5f + 0.5f * (float)Math.Sin(_t * 1.3f + s);
                    canvas.DrawCircle(sx, sy, 1.1f + 0.8f * tw, star);
                }

        canvas.Restore();

        // Marco + parteluz (cruz)
        using (var frame = new SKPaint { IsAntialias = true, Color = new SKColor(0x12, 0x0E, 0x24), StrokeWidth = 3, Style = SKPaintStyle.Stroke })
        {
            canvas.DrawPath(win, frame);
            canvas.DrawLine(new SKPoint((p0.X + p1.X) / 2f, (p0.Y + p1.Y) / 2f), new SKPoint((p3.X + p2.X) / 2f, (p3.Y + p2.Y) / 2f), frame);
            canvas.DrawLine(new SKPoint((p0.X + p3.X) / 2f, (p0.Y + p3.Y) / 2f), new SKPoint((p1.X + p2.X) / 2f, (p1.Y + p2.Y) / 2f), frame);
        }

        // Cortinas laterales que se mecen (borde inferior oscila)
        float sway = (float)Math.Sin(_t * 1.1f) * (p1.X - p0.X) * 0.05f;
        float panelW = (p1.X - p0.X) * 0.16f;
        using (var curtain = new SKPaint { IsAntialias = true, Color = new SKColor(0x7A, 0x32, 0x55, 175) })
        {
            canvas.DrawPath(Quad(p0, new SKPoint(p0.X + panelW, p0.Y), new SKPoint(p3.X + panelW + sway, p3.Y), p3), curtain);
            canvas.DrawPath(Quad(new SKPoint(p1.X - panelW, p1.Y), p1, p2, new SKPoint(p2.X - panelW + sway, p2.Y)), curtain);
        }
    }

    // ---------- Planta con hojas que se mecen ----------
    void DrawPlant(SKCanvas canvas, SKPoint baseP, float tileW)
    {
        float potW = tileW * 0.2f, potH = tileW * 0.16f;
        SoftShadow(canvas, baseP.X, baseP.Y + 2, potW * 1.1f, potH * 0.5f, 70, 6);

        using (var pot = new SKPaint { IsAntialias = true, Color = new SKColor(0xC8, 0x6E, 0x4B) })
        {
            var path = new SKPath();
            path.MoveTo(baseP.X - potW * 0.5f, baseP.Y - potH);
            path.LineTo(baseP.X + potW * 0.5f, baseP.Y - potH);
            path.LineTo(baseP.X + potW * 0.38f, baseP.Y);
            path.LineTo(baseP.X - potW * 0.38f, baseP.Y);
            path.Close();
            canvas.DrawPath(path, pot);
        }

        var topP = new SKPoint(baseP.X, baseP.Y - potH);
        const int leaves = 5;
        using var leaf = new SKPaint { IsAntialias = true, Color = new SKColor(0x4E, 0xC0, 0x7E) };
        for (int k = 0; k < leaves; k++)
        {
            float baseAngle = -70 + 140f * k / (leaves - 1); // abanico
            float sway = (float)Math.Sin(_t * 1.4f + k) * 4f;
            canvas.Save();
            canvas.Translate(topP.X, topP.Y);
            canvas.RotateDegrees(baseAngle + sway);
            float lh = tileW * (0.34f + 0.05f * (k % 2));
            canvas.DrawOval(new SKRect(-tileW * 0.045f, -lh, tileW * 0.045f, 0), leaf);
            canvas.Restore();
        }
    }

    // ---------- Muebles comprables (se dibujan en slots fijos si se poseen) ----------
    void DrawLamp(SKCanvas canvas, SKPoint baseP, float tileW, Sky sky)
    {
        float poleH = tileW * 0.62f;
        float cx = baseP.X, sy = baseP.Y - poleH;
        SoftShadow(canvas, cx, baseP.Y + 2, tileW * 0.14f, tileW * 0.06f, 70, 6);
        // brillo de la pantalla (más fuerte de noche)
        using (var glow = new SKPaint { IsAntialias = true, Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, sy), tileW * 0.55f,
                new[] { new SKColor(255, 214, 150, (byte)(50 + 80 * (1 - sky.Light))), new SKColor(255, 214, 150, 0) },
                new[] { 0f, 1f }, SKShaderTileMode.Clamp) })
            canvas.DrawCircle(cx, sy, tileW * 0.55f, glow);
        using (var pole = new SKPaint { IsAntialias = true, Color = new SKColor(0x3A, 0x33, 0x55) })
            canvas.DrawRect(new SKRect(cx - tileW * 0.012f, sy, cx + tileW * 0.012f, baseP.Y), pole);
        using (var foot = new SKPaint { IsAntialias = true, Color = new SKColor(0x2A, 0x24, 0x42) })
            canvas.DrawOval(new SKRect(cx - tileW * 0.06f, baseP.Y - tileW * 0.02f, cx + tileW * 0.06f, baseP.Y + tileW * 0.03f), foot);
        float sw = tileW * 0.16f, sh = tileW * 0.14f;
        using (var shade = new SKPaint { IsAntialias = true, Color = new SKColor(255, 226, 170) })
            canvas.DrawPath(Quad(
                new SKPoint(cx - sw * 0.5f, sy - sh), new SKPoint(cx + sw * 0.5f, sy - sh),
                new SKPoint(cx + sw * 0.72f, sy), new SKPoint(cx - sw * 0.72f, sy)), shade);
    }

    void DrawPoster(SKCanvas canvas, SKPoint a, SKPoint b, float wallH)
    {
        var bl = new SKPoint(a.X, a.Y - wallH * 0.30f);
        var br = new SKPoint(b.X, b.Y - wallH * 0.30f);
        var tr = new SKPoint(b.X, b.Y - wallH * 0.72f);
        var tl = new SKPoint(a.X, a.Y - wallH * 0.72f);
        var art = Quad(tl, tr, br, bl);
        using (var fill = new SKPaint { IsAntialias = true, Shader = SKShader.CreateLinearGradient(
                new SKPoint(tl.X, tl.Y), new SKPoint(br.X, br.Y),
                new[] { new SKColor(0x4E, 0xC0, 0xB5), new SKColor(0xFF, 0x8F, 0xB0) }, null, SKShaderTileMode.Clamp) })
            canvas.DrawPath(art, fill);
        using (var frame = new SKPaint { IsAntialias = true, Color = new SKColor(0x14, 0x10, 0x28), Style = SKPaintStyle.Stroke, StrokeWidth = 3 })
            canvas.DrawPath(art, frame);
    }

    void DrawClock(SKCanvas canvas, SKPoint baseR, float wallH)
    {
        var c = new SKPoint(baseR.X, baseR.Y - wallH * 0.58f);
        float r = wallH * 0.22f;
        using (var face = new SKPaint { IsAntialias = true, Color = new SKColor(0xF4, 0xEC, 0xD8) })
            canvas.DrawCircle(c, r, face);
        using (var rim = new SKPaint { IsAntialias = true, Color = new SKColor(0x2A, 0x22, 0x42), Style = SKPaintStyle.Stroke, StrokeWidth = 2.5f })
            canvas.DrawCircle(c, r, rim);
        var now = DateTime.Now;
        float minA = (float)(now.Minute / 60.0 * 2 * Math.PI);
        float hourA = (float)(((now.Hour % 12) + now.Minute / 60.0) / 12.0 * 2 * Math.PI);
        using var hand = new SKPaint { IsAntialias = true, Color = new SKColor(0x2A, 0x22, 0x42), Style = SKPaintStyle.Stroke, StrokeWidth = 2, StrokeCap = SKStrokeCap.Round };
        canvas.DrawLine(c, new SKPoint(c.X + r * 0.5f * (float)Math.Sin(hourA), c.Y - r * 0.5f * (float)Math.Cos(hourA)), hand);
        canvas.DrawLine(c, new SKPoint(c.X + r * 0.78f * (float)Math.Sin(minA), c.Y - r * 0.78f * (float)Math.Cos(minA)), hand);
        using (var hub = new SKPaint { IsAntialias = true, Color = new SKColor(0x2A, 0x22, 0x42) })
            canvas.DrawCircle(c, 2f, hub);
    }

    // ---------- Día / noche (según hora local del dispositivo) ----------
    struct Sky { public SKColor Top, Bottom; public float Light, Warm; }

    static Sky DayNight()
    {
        double h = DateTime.Now.TimeOfDay.TotalHours;
        float light = (h < 6 || h > 18) ? 0.05f : (float)Math.Sin(Math.PI * (h - 6) / 12);
        light = Math.Clamp(light, 0.05f, 1f);
        float warm = Math.Max(Bump(h, 7.0, 2.2), Bump(h, 18.0, 2.2));

        var nightTop = new SKColor(0x16, 0x1B, 0x3E); var nightBot = new SKColor(0x0B, 0x0E, 0x24);
        var dayTop = new SKColor(0x6F, 0xB0, 0xE8); var dayBot = new SKColor(0xC2, 0xDC, 0xF2);
        var duskTop = new SKColor(0xF2, 0x8C, 0x5A); var duskBot = new SKColor(0xF6, 0xC8, 0x8C);

        var t = LerpC(LerpC(nightTop, dayTop, light), duskTop, warm * 0.7f);
        var b = LerpC(LerpC(nightBot, dayBot, light), duskBot, warm * 0.7f);
        return new Sky { Top = t, Bottom = b, Light = light, Warm = warm };

        static float Bump(double x, double c, double w) => (float)Math.Exp(-((x - c) * (x - c)) / (2 * w * w));
    }

    // ---------- Paleta por estilo (cosmético comprado en la tienda) ----------
    struct RoomPalette { public SKColor WallLTop, WallLBot, WallRTop, WallRBot, Floor, Rug, BedTop, BedSide, BedSideDark; }

    static RoomPalette Palette(string? style) => style switch
    {
        "forest" => new RoomPalette
        {
            WallLTop = new(0x2C, 0x3D, 0x2A), WallLBot = new(0x16, 0x24, 0x14),
            WallRTop = new(0x3C, 0x52, 0x36), WallRBot = new(0x22, 0x32, 0x1E),
            Floor = new(0x4A, 0x39, 0x28), Rug = new(0x8F, 0xC4, 0x5F),
            BedTop = new(0x77, 0xC4, 0x6A), BedSide = new(0x5F, 0xA8, 0x6A), BedSideDark = new(0x49, 0x88, 0x52)
        },
        "galaxy" => new RoomPalette
        {
            WallLTop = new(0x24, 0x1F, 0x4E), WallLBot = new(0x10, 0x0C, 0x28),
            WallRTop = new(0x34, 0x2B, 0x68), WallRBot = new(0x1C, 0x14, 0x42),
            Floor = new(0x2A, 0x22, 0x55), Rug = new(0xB9, 0x6B, 0xFF),
            BedTop = new(0x9B, 0x6B, 0xFF), BedSide = new(0x6A, 0x5F, 0xD6), BedSideDark = new(0x52, 0x49, 0xA8)
        },
        _ => new RoomPalette // default (morado cozy actual)
        {
            WallLTop = new(0x2A, 0x1F, 0x4E), WallLBot = new(0x17, 0x10, 0x30),
            WallRTop = new(0x3A, 0x2B, 0x68), WallRBot = new(0x22, 0x18, 0x42),
            Floor = new(0x36, 0x29, 0x5E), Rug = new(0xFF, 0x5F, 0x8F),
            BedTop = new(0xFF, 0x52, 0x77), BedSide = new(0x6A, 0x5F, 0xD6), BedSideDark = new(0x52, 0x49, 0xA8)
        },
    };

    // ---------- Helpers de dibujo ----------
    static SKColor LerpC(SKColor a, SKColor b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new SKColor(
            (byte)(a.Red + (b.Red - a.Red) * t),
            (byte)(a.Green + (b.Green - a.Green) * t),
            (byte)(a.Blue + (b.Blue - a.Blue) * t));
    }

    static SKColor Scale(SKColor c, float f)
    {
        f = Math.Clamp(f, 0f, 1f);
        return new SKColor((byte)(c.Red * f), (byte)(c.Green * f), (byte)(c.Blue * f));
    }

    static SKColor Offset(SKColor c, int d) => new(
        (byte)Math.Min(255, c.Red + d), (byte)Math.Min(255, c.Green + d), (byte)Math.Min(255, c.Blue + d));

    static SKPath Quad(SKPoint a, SKPoint b, SKPoint c, SKPoint d)
    {
        var path = new SKPath();
        path.MoveTo(a); path.LineTo(b); path.LineTo(c); path.LineTo(d); path.Close();
        return path;
    }

    static void DrawWall(SKCanvas canvas, SKPoint floor0, SKPoint floor1, float h, SKColor top, SKColor bottom)
    {
        var t0 = new SKPoint(floor0.X, floor0.Y - h);
        var t1 = new SKPoint(floor1.X, floor1.Y - h);
        using var p = new SKPaint { IsAntialias = true, Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, floor0.Y - h), new SKPoint(0, Math.Max(floor0.Y, floor1.Y)),
                new[] { top, bottom }, null, SKShaderTileMode.Clamp) };
        canvas.DrawPath(Quad(floor0, floor1, t1, t0), p);
        using var hi = new SKPaint { IsAntialias = true, Color = new SKColor(255, 255, 255, 18) };
        canvas.DrawPath(Quad(t0, t1, new SKPoint(t1.X, t1.Y + 5), new SKPoint(t0.X, t0.Y + 5)), hi);
    }

    // Los SKPaint y los filtros de desenfoque se reutilizan en vez de crearse por sombra y por frame.
    // A 25 fps eso eran decenas de objetos nativos por segundo: no subía la media de pintado (8 ms de 40),
    // pero la basura que generaba provocaba pausas de GC — los picos de 25-30 ms que se veían como tirones.
    // Los SKMaskFilter son inmutables y solo se usan 2-3 radios, así que se cachean por radio.
    static readonly Dictionary<float, SKMaskFilter> BlurCache = new();
    static SKMaskFilter Blur(float radius)
    {
        if (!BlurCache.TryGetValue(radius, out var f))
            BlurCache[radius] = f = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, radius);
        return f;
    }

    static readonly SKPaint ShadowPaint = new() { IsAntialias = true };
    static readonly SKPaint DustPaint = new() { IsAntialias = true };
    static readonly SKPaint FlatPaint = new();   // rellenos planos (tintes de día/noche, escarcha)

    static void SoftShadow(SKCanvas canvas, float cx, float cy, float rx, float ry, byte alpha, float blur)
    {
        ShadowPaint.Color = new SKColor(0, 0, 0, alpha);
        ShadowPaint.MaskFilter = Blur(blur);
        canvas.DrawOval(new SKRect(cx - rx, cy - ry, cx + rx, cy + ry), ShadowPaint);
    }

    static void DrawBox(SKCanvas canvas, SKPoint a, SKPoint b, SKPoint c, SKPoint d, float h, SKColor top, SKColor side, SKColor sideDark)
    {
        SKPoint U(SKPoint p) => new(p.X, p.Y - h);
        using var s1 = new SKPaint { IsAntialias = true, Color = sideDark };
        canvas.DrawPath(Quad(d, c, U(c), U(d)), s1);
        using var s2 = new SKPaint { IsAntialias = true, Color = side };
        canvas.DrawPath(Quad(b, c, U(c), U(b)), s2);
        using var tp = new SKPaint { IsAntialias = true, Color = top };
        canvas.DrawPath(Quad(U(a), U(b), U(c), U(d)), tp);
    }
}
