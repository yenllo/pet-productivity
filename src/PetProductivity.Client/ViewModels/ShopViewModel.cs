using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using PetProductivity.Shared.Models;

using PetProductivity.Client.Services;

namespace PetProductivity.Client.ViewModels
{
    public partial class ShopViewModel : ObservableObject
    {
        private readonly Services.GameDataService _gameDataService;
        private List<ShopItem> _catalog = new();
        private string _selectedCategory = "Todo";

        [ObservableProperty]
        private ObservableCollection<ShopItemVm> items;

        // Chips de categoría (cableados: filtran el catálogo). "Todo" + categorías del catálogo.
        [ObservableProperty]
        private ObservableCollection<CategoryVm> categories;

        [ObservableProperty]
        private int userGold;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowStatus))]
        private string statusMessage = string.Empty;

        public bool ShowStatus => !IsLoading && !string.IsNullOrEmpty(StatusMessage);

        // ---- Buscador + paginación (para no renderizar cientos de tarjetas de golpe) ----
        // Vista compacta (muebles sin descripción): rejilla 3xN, solo imagen+precio, 9 por página.
        // Vista detallada (consumibles/cosmético/estilos/premium/eventos): 2 columnas, tarjeta completa, 4/pág.
        private int _pageSize = 9;
        private List<ShopItem> _filtered = new();       // catálogo tras categoría + búsqueda

        // Categorías que se muestran como tarjeta detallada (tienen descripción/estado). El resto = compacto.
        private static readonly HashSet<string> DetailedCats = new() { "Consumibles", "Cosmético", "Estilos", "Premium", "Eventos" };

        // T27-L3 (#21): orden fijo de categorías (antes: orden de carpeta del catálogo). Lo no listado va al final.
        private static readonly string[] CatOrder = { "Todo", "Muebles", "Decoración", "Vida", "Estructural", "Estilos", "Consumibles", "Cosmético", "Eventos", "Premium" };
        private static int CatRank(string c) { var i = Array.IndexOf(CatOrder, c); return i < 0 ? int.MaxValue : i; }

        // true = rejilla compacta de muebles; false = tarjetas detalladas. Controla qué CollectionView se ve.
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDetailedView))]
        private bool isCompactView = true;

        public bool IsDetailedView => !IsCompactView;

        // Texto del buscador (filtra por nombre/categoría). Al cambiar, vuelve a la página 1.
        [ObservableProperty]
        private string searchText = string.Empty;

        partial void OnSearchTextChanged(string value) => ApplyFilters();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageInfo))]
        [NotifyPropertyChangedFor(nameof(CanPrev))]
        [NotifyPropertyChangedFor(nameof(CanNext))]
        [NotifyPropertyChangedFor(nameof(HasPages))]
        private int currentPage = 1;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PageInfo))]
        [NotifyPropertyChangedFor(nameof(CanPrev))]
        [NotifyPropertyChangedFor(nameof(CanNext))]
        [NotifyPropertyChangedFor(nameof(HasPages))]
        private int totalPages = 1;

        public bool HasPages => TotalPages > 1;
        public bool CanPrev => CurrentPage > 1;
        public bool CanNext => CurrentPage < TotalPages;
        public string PageInfo => L.F("Página {0} de {1}", CurrentPage, TotalPages);

        [RelayCommand]
        private void NextPage() { if (CanNext) { CurrentPage++; RenderPage(); } }

        [RelayCommand]
        private void PrevPage() { if (CanPrev) { CurrentPage--; RenderPage(); } }

        public ShopViewModel(Services.GameDataService gameDataService)
        {
            _gameDataService = gameDataService;
            Items = new ObservableCollection<ShopItemVm>();
            Categories = new ObservableCollection<CategoryVm>();
        }

        public async Task InitializeAsync()
        {
            IsLoading = true;
            StatusMessage = string.Empty;
            try
            {
                await _gameDataService.InitializeAsync();
                UserGold = _gameDataService.GetGold();
                _catalog = await _gameDataService.GetCatalogAsync() ?? new();
                await EnsureSpriteCacheAsync(_catalog); // #23: copia única de assets → FromFile (Glide cachea; FromStream no)
                BuildCategories();
                ApplyFilters();
                if (_catalog.Count == 0) StatusMessage = L.T("La tienda está vacía por ahora.");
            }
            catch
            {
                StatusMessage = L.T("No se pudo cargar la tienda. Revisa tu conexión.");
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(ShowStatus));
            }
        }

        // #23: los sprites del catálogo (MauiAsset) se copian una vez a CacheDirectory; los que ya existen se saltan.
        private static async Task EnsureSpriteCacheAsync(List<ShopItem> catalog)
        {
            foreach (var id in catalog.Where(i => !string.IsNullOrEmpty(i.SpriteId)).Select(i => i.SpriteId).Distinct())
            {
                var dest = Path.Combine(FileSystem.CacheDirectory, $"{id}.png");
                if (File.Exists(dest)) continue;
                try
                {
                    using var src = await FileSystem.OpenAppPackageFileAsync($"{id}.png");
                    using var dst = File.Create(dest);
                    await src.CopyToAsync(dst);
                }
                catch { /* sin sprite en el paquete: la tarjeta cae al emoji */ }
            }
        }

        private void BuildCategories()
        {
            Categories.Clear();
            Categories.Add(new CategoryVm("Todo", _selectedCategory == "Todo"));
            foreach (var c in _catalog.Select(i => i.Category).Where(c => !string.IsNullOrEmpty(c)).Distinct()
                                      .OrderBy(CatRank).ThenBy(c => c))
                Categories.Add(new CategoryVm(c, _selectedCategory == c));
        }

        [RelayCommand]
        private void SelectCategory(CategoryVm cat)
        {
            if (cat == null) return;
            _selectedCategory = cat.Name;
            foreach (var c in Categories) c.IsSelected = c.Name == cat.Name;
            ApplyFilters();
        }

        // Recalcula el conjunto filtrado (categoría + búsqueda) y vuelve a la primera página.
        private void ApplyFilters()
        {
            var q = _catalog.AsEnumerable();
            if (_selectedCategory != "Todo")
                q = q.Where(i => i.Category == _selectedCategory);

            var s = (SearchText ?? string.Empty).Trim();
            if (s.Length > 0)
                q = q.Where(i =>
                    i.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    i.Category.Contains(s, StringComparison.OrdinalIgnoreCase));

            // #21: agrupado por categoría (mismo orden que los chips) y barato→caro dentro de cada una.
            _filtered = q.OrderBy(i => CatRank(i.Category)).ThenBy(i => i.Price).ThenBy(i => i.Name).ToList();

            // Layout según categoría: detallada (2x2, con info) vs compacta (rejilla de muebles, solo imagen).
            bool detailed = DetailedCats.Contains(_selectedCategory);
            IsCompactView = !detailed;
            _pageSize = detailed ? 4 : 9;

            CurrentPage = 1;
            RenderPage();
        }

        // Muestra solo la página actual (_pageSize ítems), no todo el catálogo de golpe.
        private void RenderPage()
        {
            TotalPages = Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)_pageSize));
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;

            var inv = _gameDataService.CurrentUser?.Inventory ?? new();
            var active = _gameDataService.GetActiveStyle();

            Items.Clear();
            foreach (var item in _filtered.Skip((CurrentPage - 1) * _pageSize).Take(_pageSize))
                Items.Add(new ShopItemVm(item, owned: inv.ContainsKey(item.Name), gold: UserGold, activeStyle: active));

            if (_catalog.Count > 0)
                StatusMessage = _filtered.Count == 0 ? L.T("No se encontraron objetos.") : string.Empty;
            OnPropertyChanged(nameof(ShowStatus));
        }

        // Comprar (si no se posee) o equipar (si es un estilo ya poseído).
        [RelayCommand]
        public async Task Act(ShopItemVm vm)
        {
            if (vm == null || !vm.CanAct) return;
            if (vm.IsPremium && !vm.Owned)
            {
                var (ok, msg) = await _gameDataService.PurchasePremiumAsync(vm.Item.Name);
                if (!ok) { await Toast.Make(msg).Show(); return; }
                await Toast.Make(L.F("✓ {0} desbloqueado", vm.Item.Name)).Show();
            }
            else if (!vm.Owned)
            {
                var (ok, msg) = await _gameDataService.BuyItemAsync(vm.Item.Name, vm.Item.Price, vm.Item.Description);
                if (!ok) { await Toast.Make(msg).Show(); return; }
                // Mueble con sprite → se coloca solo en el cuarto (F5.2). El resto solo va al inventario.
                if (!string.IsNullOrEmpty(vm.Item.SpriteId))
                {
                    bool placed = await _gameDataService.AutoPlaceAsync(vm.Item);
                    await Toast.Make(placed
                        ? L.F("✓ {0} colocado en tu cuarto", vm.Item.Name)
                        : L.F("✓ Comprado. Cuarto lleno: {0} quedó en Guardados (lápiz ✏️ del cuarto)", vm.Item.Name),
                        CommunityToolkit.Maui.Core.ToastDuration.Long).Show();
                }
                else
                    await Toast.Make(L.F("✓ Has comprado {0}", vm.Item.Name)).Show();
            }
            else // estilo poseído → equipar
            {
                var (ok, msg) = await _gameDataService.EquipStyleAsync(vm.Item.StyleKey);
                if (!ok) { await Toast.Make(msg).Show(); return; }
                await Toast.Make(L.T("✓ Estilo equipado")).Show();
            }
            RefreshAll();
        }

        private void RefreshAll()
        {
            UserGold = _gameDataService.GetGold();
            var inv = _gameDataService.CurrentUser?.Inventory ?? new();
            var active = _gameDataService.GetActiveStyle();
            foreach (var i in Items) i.Refresh(inv.ContainsKey(i.Item.Name), UserGold, active);
        }
    }

    // Chip de categoría (nombre + estado seleccionado) para el filtro de la tienda.
    public partial class CategoryVm : ObservableObject
    {
        public string Name { get; }
        public string DisplayName => L.T(Name); // #26: display; el filtro usa Name

        [ObservableProperty]
        private bool isSelected;

        public CategoryVm(string name, bool selected)
        {
            Name = name;
            IsSelected = selected;
        }
    }

    // View-model por ítem: añade sprite + estado (poseído / asequible / estilo activo) sin tocar el modelo Shared.
    public partial class ShopItemVm : ObservableObject
    {
        public ShopItem Item { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanAct))]
        [NotifyPropertyChangedFor(nameof(PriceLabel))]
        [NotifyPropertyChangedFor(nameof(CompactPrice))]
        [NotifyPropertyChangedFor(nameof(Affordable))]
        private bool owned;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanAct))]
        [NotifyPropertyChangedFor(nameof(PriceLabel))]
        [NotifyPropertyChangedFor(nameof(CompactPrice))]
        [NotifyPropertyChangedFor(nameof(Affordable))]
        private bool canAfford;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanAct))]
        [NotifyPropertyChangedFor(nameof(PriceLabel))]
        [NotifyPropertyChangedFor(nameof(CompactPrice))]
        [NotifyPropertyChangedFor(nameof(Affordable))]
        private bool isActiveStyle;

        public ShopItemVm(ShopItem item, bool owned, int gold, string activeStyle)
        {
            Item = item;
            Owned = owned;
            CanAfford = gold >= item.Price;
            IsActiveStyle = IsStyle && Item.StyleKey == activeStyle;
        }

        public void Refresh(bool owned, int gold, string activeStyle)
        {
            Owned = owned;
            CanAfford = gold >= Item.Price;
            IsActiveStyle = IsStyle && Item.StyleKey == activeStyle;
        }

        public string Name => Item.Name;
        public string Description => Item.Description;
        public string Emoji => Item.Icon;
        public string Category => Item.Category;
        public bool IsStyle => !string.IsNullOrEmpty(Item.StyleKey);
        public bool IsPremium => Item.Currency == "Premium";

        // Rareza: solo se muestra el badge si no es común.
        public string RarityLabel => Item.Rarity;
        public bool ShowRarity => !string.IsNullOrEmpty(Item.Rarity) && Item.Rarity != "Common";

        // Colaboración: crédito de origen.
        public bool ShowSource => !string.IsNullOrEmpty(Item.Source);
        public string SourceLabel => Item.Source;

        // Evento (F5.3): contador de tiempo restante hasta AvailableTo.
        public bool ShowCountdown => Item.AvailableTo != null && !Owned;
        public string CountdownLabel
        {
            get
            {
                if (Item.AvailableTo == null) return string.Empty;
                var left = Item.AvailableTo.Value.ToUniversalTime() - DateTime.UtcNow;
                if (left <= TimeSpan.Zero) return L.T("Terminado");
                if (left.TotalDays >= 1) return L.F("⏳ termina en {0}d", (int)left.TotalDays);
                if (left.TotalHours >= 1) return L.F("⏳ termina en {0}h", (int)left.TotalHours);
                return L.F("⏳ termina en {0}m", (int)left.TotalMinutes);
            }
        }

        // Sprite de la tarjeta: obj_<id> del pack (Resources/Raw, MauiAsset) vía stream; si no, sprite spr_ de
        // la tienda (MauiImage); si tampoco, se muestra el emoji.
        private ImageSource? _spriteCache;
        private bool _spriteBuilt;
        public ImageSource? SpriteImageSource
        {
            get
            {
                if (_spriteBuilt) return _spriteCache;   // una sola ImageSource por ítem (evita recargar al hacer scroll)
                _spriteBuilt = true;
                if (!string.IsNullOrEmpty(Item.SpriteId))
                {
                    // #23: preferir la copia en CacheDirectory (FromFile → Glide cachea entre páginas/scroll).
                    var cached = Path.Combine(FileSystem.CacheDirectory, $"{Item.SpriteId}.png");
                    _spriteCache = File.Exists(cached)
                        ? ImageSource.FromFile(cached)
                        : ImageSource.FromStream(ct => FileSystem.OpenAppPackageFileAsync($"{Item.SpriteId}.png"));
                }
                else { var spr = MapSprite(Item.Name); _spriteCache = string.IsNullOrEmpty(spr) ? null : ImageSource.FromFile(spr); }
                return _spriteCache;
            }
        }
        public bool HasSprite => !string.IsNullOrEmpty(Item.SpriteId) || !string.IsNullOrEmpty(MapSprite(Item.Name));
        public bool NoSprite => !HasSprite;

        // Puede comprar (premium: siempre; oro: si alcanza) o equipar (estilo poseído que no está activo).
        public bool CanAct => (!Owned && (IsPremium || CanAfford)) || (Owned && IsStyle && !IsActiveStyle);

        // Etiqueta de precio en la tarjeta detallada: SIEMPRE número + (icono moneda en XAML); nunca "Faltan".
        public string PriceLabel =>
            IsPremium && !Owned ? $"💎 ${Item.Price / 100.0:0.00}"
            : !Owned ? Item.Price.ToString()
            : IsStyle ? (IsActiveStyle ? L.T("Equipado ✓") : L.T("Equipar"))
            : L.T("Comprado");

        // Precio corto para la rejilla compacta (solo imagen): número, o ✓ si ya se posee.
        public string CompactPrice =>
            Owned ? (IsStyle && !IsActiveStyle ? L.T("Equipar") : "✓")
            : IsPremium ? $"${Item.Price / 100.0:0.00}"
            : Item.Price.ToString();

        // true = tengo oro suficiente para comprarlo (aún no poseído) → fondo amarillo duro; si no, gris.
        public bool Affordable => !Owned && CanAfford;

        // Mapea el nombre del ítem a un sprite pixel de tienda (spr_*); vacío = sin sprite.
        private static string MapSprite(string name)
        {
            var n = (name ?? string.Empty).ToLowerInvariant();
            if (n.Contains("poci") || n.Contains("poti")) return "spr_potion.png";
            if (n.Contains("crist") || n.Contains("gema") || n.Contains("gem")) return "spr_gem.png";
            if (n.Contains("sombrero") || n.Contains("hat")) return "spr_hat.png";
            if (n.Contains("corona") || n.Contains("crown")) return "spr_crown.png";
            if (n.Contains("cupcake") || n.Contains("pastel")) return "spr_cupcake.png";
            if (n.Contains("manzana") || n.Contains("fruta") || n.Contains("comida") || n.Contains("apple")) return "spr_apple.png";
            if (n.Contains("oro") || n.Contains("moneda") || n.Contains("coin")) return "spr_coin.png";
            return string.Empty;
        }
    }
}
