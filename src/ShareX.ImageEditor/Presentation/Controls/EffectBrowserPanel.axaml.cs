using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.ImageEditor.Hosting;
using ShareX.ImageEditor.Presentation.Filters;
using ShareX.ImageEditor.Presentation.Theming;
using System.Collections.ObjectModel;
using System.Text;

namespace ShareX.ImageEditor.Presentation.Controls
{
    public sealed class EffectDialogRequestedEventArgs : EventArgs
    {
        public string EffectId { get; }
        public EffectDialogRequestedEventArgs(string effectId) => EffectId = effectId;
    }

    public partial class EffectCategory : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        private readonly bool _showCount;
        private readonly bool _keepVisibleWhenEmpty;
        private readonly string? _headerHint;

        public ObservableCollection<EffectItem> AllEffects { get; } = new();

        [ObservableProperty]
        private ObservableCollection<EffectItem> _visibleEffects = new();

        [ObservableProperty]
        private bool _isVisible = true;

        public EffectCategory(string name, bool showCount = true, bool keepVisibleWhenEmpty = false, string? headerHint = null)
        {
            Name = name;
            _showCount = showCount;
            _keepVisibleWhenEmpty = keepVisibleWhenEmpty;
            _headerHint = headerHint;
        }

        public string HeaderText
        {
            get
            {
                string header = _showCount ? $"{Name} ({AllEffects.Count})" : Name;
                return string.IsNullOrWhiteSpace(_headerHint) ? header : $"{header} - {_headerHint}";
            }
        }

        partial void OnNameChanged(string value)
        {
            OnPropertyChanged(nameof(HeaderText));
        }

        public EffectItem AddEffect(string name, string icon, string description, Action execute, string? effectId = null, bool keepSorted = true)
        {
            var effect = new EffectItem(name, icon, description, execute, effectId);
            Insert(AllEffects, effect, keepSorted);
            Insert(VisibleEffects, effect, keepSorted);
            OnPropertyChanged(nameof(HeaderText));
            return effect;
        }

        public EffectItem AddEffectCopy(EffectItem effect, bool keepSorted = true)
            => AddEffect(effect.Name, effect.Icon, effect.Description, effect.ExecuteAction, effect.EffectId, keepSorted)
                .WithExecuteObserver(effect.ExecuteObserver);

        public void ClearEffects()
        {
            AllEffects.Clear();
            VisibleEffects.Clear();
            OnPropertyChanged(nameof(HeaderText));
        }

        public bool RemoveEffectsById(string effectId)
        {
            bool removed = false;

            for (int i = AllEffects.Count - 1; i >= 0; i--)
            {
                if (string.Equals(AllEffects[i].EffectId, effectId, StringComparison.OrdinalIgnoreCase))
                {
                    AllEffects.RemoveAt(i);
                    removed = true;
                }
            }

            for (int i = VisibleEffects.Count - 1; i >= 0; i--)
            {
                if (string.Equals(VisibleEffects[i].EffectId, effectId, StringComparison.OrdinalIgnoreCase))
                {
                    VisibleEffects.RemoveAt(i);
                }
            }

            if (removed)
            {
                OnPropertyChanged(nameof(HeaderText));
            }

            return removed;
        }

        private static void Insert(ObservableCollection<EffectItem> target, EffectItem effect, bool keepSorted)
        {
            if (!keepSorted)
            {
                target.Add(effect);
                return;
            }

            int index = 0;
            while (index < target.Count &&
                   string.Compare(target[index].Name, effect.Name, StringComparison.OrdinalIgnoreCase) <= 0)
            {
                index++;
            }

            target.Insert(index, effect);
        }

        public void Filter(string searchText)
        {
            VisibleEffects.Clear();
            if (string.IsNullOrWhiteSpace(searchText))
            {
                foreach (var effect in AllEffects) VisibleEffects.Add(effect);
            }
            else
            {
                foreach (var effect in AllEffects)
                {
                    if (effect.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    {
                        VisibleEffects.Add(effect);
                    }
                }
            }

            IsVisible = VisibleEffects.Count > 0 || (_keepVisibleWhenEmpty && string.IsNullOrWhiteSpace(searchText));
        }
    }

    public partial class EffectItem : ObservableObject
    {
        [ObservableProperty]
        private string _effectId;

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _icon;

        [ObservableProperty]
        private string _description;

        public Action ExecuteAction { get; }
        public Action<EffectItem>? ExecuteObserver { get; set; }

        public EffectItem(string name, string icon, string description, Action executeAction, string? effectId = null)
        {
            EffectId = string.IsNullOrWhiteSpace(effectId) ? NormalizeEffectId(name) : effectId;
            Name = name;
            Icon = icon;
            Description = description;
            ExecuteAction = executeAction;
        }

        public EffectItem WithExecuteObserver(Action<EffectItem>? executeObserver)
        {
            ExecuteObserver = executeObserver;
            return this;
        }

        public static string NormalizeEffectId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(value.Length);
            bool lastWasUnderscore = false;

            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                    lastWasUnderscore = false;
                    continue;
                }

                if (!lastWasUnderscore)
                {
                    sb.Append('_');
                    lastWasUnderscore = true;
                }
            }

            while (sb.Length > 0 && sb[0] == '_')
            {
                sb.Remove(0, 1);
            }

            while (sb.Length > 0 && sb[^1] == '_')
            {
                sb.Length--;
            }

            return sb.ToString();
        }

        [RelayCommand]
        private void Execute()
        {
            ExecuteAction?.Invoke();
            ExecuteObserver?.Invoke(this);
        }
    }

    public partial class EffectBrowserPanel : UserControl
    {
        private const string FavoritesHeaderHint = "Right click to favorite";
        private const int MaxRecentEffects = 10;
        private const string SearchWatermarkFormat = "Search image effects... ({0})";

        private static readonly Dictionary<string, string> EffectAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["resize"] = "resize_image",
            ["canvas"] = "resize_canvas",
            ["crop"] = "crop_image",
            ["auto_crop"] = "auto_crop_image",
            ["rotate_90"] = "rotate_90_clockwise",
            ["rotate_90_cc"] = "rotate_90_counter_clockwise"
        };

        public event EventHandler<EffectDialogRequestedEventArgs>? EffectDialogRequested;

        public event EventHandler? InvertRequested;
        public event EventHandler? BlackAndWhiteRequested;
        public event EventHandler? PolaroidRequested;
        public event EventHandler? EdgeDetectRequested;
        public event EventHandler? EmbossRequested;
        public event EventHandler? MeanRemovalRequested;
        public event EventHandler? SmoothRequested;

        public event EventHandler? CropImageRequested;
        public event EventHandler? AutoCropImageRequested;
        public event EventHandler? Rotate90CWRequested;
        public event EventHandler? Rotate90CCWRequested;
        public event EventHandler? Rotate180Requested;
        public event EventHandler? RotateCustomAngleRequested;
        public event EventHandler? FlipHorizontalRequested;
        public event EventHandler? FlipVerticalRequested;

        public ObservableCollection<EffectCategory> Categories { get; } = new();

        private readonly EffectCategory _recentCategory = new("Recent");
        private readonly EffectCategory _favoritesCategory = new("Favorites", headerHint: FavoritesHeaderHint);
        private readonly Dictionary<string, EffectItem> _allEffectsById = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _recentEffectIds = new();
        private readonly HashSet<string> _favoriteEffectIds = new(StringComparer.OrdinalIgnoreCase);
        private ImageEditorOptions? _options;

        public EffectBrowserPanel()
        {
            AvaloniaXamlLoader.Load(this);
            InitializeEffects();
            BuildEffectLookup();
            LoadFavoriteEffects(ImageEditorOptions.DefaultFavoriteEffects, persistToOptions: false);

            var categoriesControl = this.FindControl<ItemsControl>("CategoriesControl");
            if (categoriesControl != null)
            {
                categoriesControl.ItemsSource = Categories;
            }

            UpdateSearchWatermark();
        }

        public void SetOptions(ImageEditorOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            LoadRecentEffects(options.RecentEffects, persistToOptions: true);
            LoadFavoriteEffects(options.FavoriteEffects, persistToOptions: true);
        }

        private void OnSearchTextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
        {
            ApplyCurrentFilter();
        }

        public void FocusSearchBox()
        {
            var searchBox = this.FindControl<TextBox>("SearchBox");
            if (searchBox != null)
            {
                // Clear existing search
                searchBox.Text = string.Empty;
                // Focus using Dispatcher to ensure it happens after layout/visibility logic
                System.Threading.Tasks.Task.Delay(50).ContinueWith(_ =>
                {
                    Dispatcher.UIThread.Post(() => searchBox.Focus(), DispatcherPriority.Input);
                });
            }
        }

        private void Raise(EventHandler? handler)
        {
            Dispatcher.UIThread.Post(() => handler?.Invoke(this, EventArgs.Empty));
        }

        private void RaiseDialog(string effectId)
        {
            var args = new EffectDialogRequestedEventArgs(effectId);
            Dispatcher.UIThread.Post(() => EffectDialogRequested?.Invoke(this, args));
        }

        private void OnEffectItemPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not EffectItem effect)
            {
                return;
            }

            var point = e.GetCurrentPoint(button);
            if (!point.Properties.IsRightButtonPressed)
            {
                return;
            }

            if (TryToggleFavorite(effect))
            {
                ApplyCurrentFilter();
                PersistFavoritesToOptions();
            }

            e.Handled = true;
        }

        private void BuildEffectLookup()
        {
            _allEffectsById.Clear();

            foreach (var category in Categories)
            {
                if (IsPinnedCategory(category))
                {
                    continue;
                }

                foreach (var effect in category.AllEffects)
                {
                    if (!string.IsNullOrWhiteSpace(effect.EffectId))
                    {
                        effect.ExecuteObserver = RegisterRecentEffect;
                        _allEffectsById[effect.EffectId] = effect;
                    }
                }
            }
        }

        private bool IsPinnedCategory(EffectCategory category)
        {
            return ReferenceEquals(category, _recentCategory) || ReferenceEquals(category, _favoritesCategory);
        }

        private void LoadRecentEffects(IEnumerable<string>? recentEffectIds, bool persistToOptions)
        {
            _recentEffectIds.Clear();

            if (recentEffectIds != null)
            {
                foreach (string recentEffectId in recentEffectIds)
                {
                    if (!TryResolveEffect(recentEffectId, out EffectItem effect))
                    {
                        continue;
                    }

                    if (_recentEffectIds.Any(effectId => string.Equals(effectId, effect.EffectId, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    _recentEffectIds.Add(effect.EffectId);

                    if (_recentEffectIds.Count >= MaxRecentEffects)
                    {
                        break;
                    }
                }
            }

            RebuildRecentCategory();
            ApplyCurrentFilter();

            if (persistToOptions)
            {
                PersistRecentToOptions();
            }
        }

        private void LoadFavoriteEffects(IEnumerable<string>? favoriteEffectIds, bool persistToOptions)
        {
            _favoriteEffectIds.Clear();
            _favoritesCategory.ClearEffects();

            if (favoriteEffectIds != null)
            {
                foreach (string favoriteEffectId in favoriteEffectIds)
                {
                    if (TryResolveEffect(favoriteEffectId, out EffectItem effect))
                    {
                        TryAddFavorite(effect);
                    }
                }
            }

            ApplyCurrentFilter();

            if (persistToOptions)
            {
                PersistFavoritesToOptions();
            }
        }

        private bool TryResolveEffect(string value, out EffectItem effect)
        {
            effect = null!;

            string normalizedId = EffectItem.NormalizeEffectId(value);
            if (string.IsNullOrWhiteSpace(normalizedId))
            {
                return false;
            }

            if (EffectAliases.TryGetValue(normalizedId, out string? alias))
            {
                normalizedId = alias;
            }

            if (_allEffectsById.TryGetValue(normalizedId, out EffectItem? resolvedEffect) && resolvedEffect != null)
            {
                effect = resolvedEffect;
                return true;
            }

            return false;
        }

        private bool TryAddFavorite(EffectItem effect)
        {
            if (!_favoriteEffectIds.Add(effect.EffectId))
            {
                return false;
            }

            _favoritesCategory.AddEffectCopy(effect, keepSorted: false);
            return true;
        }

        private bool TryToggleFavorite(EffectItem effect)
        {
            if (_favoriteEffectIds.Contains(effect.EffectId))
            {
                return RemoveFavorite(effect.EffectId);
            }

            return TryAddFavorite(effect);
        }

        private bool RemoveFavorite(string effectId)
        {
            if (!_favoriteEffectIds.Remove(effectId))
            {
                return false;
            }

            _favoritesCategory.RemoveEffectsById(effectId);
            return true;
        }

        private void RegisterRecentEffect(EffectItem effect)
        {
            if (string.IsNullOrWhiteSpace(effect.EffectId))
            {
                return;
            }

            _recentEffectIds.RemoveAll(effectId => string.Equals(effectId, effect.EffectId, StringComparison.OrdinalIgnoreCase));
            _recentEffectIds.Insert(0, effect.EffectId);

            if (_recentEffectIds.Count > MaxRecentEffects)
            {
                _recentEffectIds.RemoveRange(MaxRecentEffects, _recentEffectIds.Count - MaxRecentEffects);
            }

            RebuildRecentCategory();
            ApplyCurrentFilter();
            PersistRecentToOptions();
        }

        private void RebuildRecentCategory()
        {
            _recentCategory.ClearEffects();

            foreach (string recentEffectId in _recentEffectIds)
            {
                if (TryResolveEffect(recentEffectId, out EffectItem effect))
                {
                    _recentCategory.AddEffectCopy(effect, keepSorted: false);
                }
            }
        }

        private void ApplyCurrentFilter()
        {
            var searchBox = this.FindControl<TextBox>("SearchBox");
            string searchText = searchBox?.Text ?? string.Empty;

            foreach (var category in Categories)
            {
                category.Filter(searchText);
            }
        }

        private void UpdateSearchWatermark()
        {
            var searchBox = this.FindControl<TextBox>("SearchBox");
            if (searchBox == null)
            {
                return;
            }

            int totalEffectCount = Categories
                .Where(category => !IsPinnedCategory(category))
                .Sum(category => category.AllEffects.Count);

            searchBox.Watermark = string.Format(SearchWatermarkFormat, totalEffectCount);
        }

        private void PersistRecentToOptions()
        {
            if (_options == null)
            {
                return;
            }

            _options.RecentEffects = _recentEffectIds.ToList();
        }

        private void PersistFavoritesToOptions()
        {
            if (_options == null)
            {
                return;
            }

            _options.FavoriteEffects = _favoritesCategory.AllEffects
                .Select(effect => effect.EffectId)
                .Where(effectId => !string.IsNullOrWhiteSpace(effectId))
                .ToList();
        }

        private void InitializeEffects()
        {
            Categories.Add(_recentCategory);
            Categories.Add(_favoritesCategory);

            var manip = new EffectCategory("Manipulations");
            manip.AddEffect("3D Box / Extrude...", LucideIcons.Box, "Applies a 3D box or extrude effect.", () => RaiseDialog("rotate_3d_box"));
            manip.AddEffect("Auto crop image", LucideIcons.Scan, "Automatically crops the image by finding its edges.", () => Raise(AutoCropImageRequested));
            manip.AddEffect("Crop image...", LucideIcons.Crop, "Crops the image.", () => Raise(CropImageRequested));
            manip.AddEffect("Displacement map...", LucideIcons.Map, "Applies a displacement map.", () => RaiseDialog("displacement_map"));
            manip.AddEffect("Flip...", LucideIcons.FlipHorizontal2, "Flip the image.", () => RaiseDialog("flip"));
            manip.AddEffect("Flip horizontal", LucideIcons.FlipHorizontal, "Flips the image horizontally.", () => Raise(FlipHorizontalRequested));
            manip.AddEffect("Flip vertical", LucideIcons.FlipVertical, "Flips the image vertically.", () => Raise(FlipVerticalRequested));
            manip.AddEffect("Perspective warp...", LucideIcons.Waypoints, "Warps the image perspective.", () => RaiseDialog("perspective_warp"));
            manip.AddEffect("Pinch / bulge...", LucideIcons.CircleGauge, "Applies a pinch or bulge effect.", () => RaiseDialog("pinch_bulge"));
            manip.AddEffect("Resize canvas...", LucideIcons.Maximize, "Resizes the canvas.", () => RaiseDialog("resize_canvas"));
            manip.AddEffect("Resize image...", LucideIcons.ImageUpscale, "Resizes the image.", () => RaiseDialog("resize_image"));
            manip.AddEffect("Rotate...", LucideIcons.RotateCw, "Rotates the image by a custom angle.", () => Raise(RotateCustomAngleRequested));
            manip.AddEffect("Rotate 180°", LucideIcons.RotateCwSquare, "Rotates the image by 180 degrees.", () => Raise(Rotate180Requested));
            manip.AddEffect("Rotate 3D...", LucideIcons.Rotate3D, "Rotates the image in 3D space.", () => RaiseDialog("rotate_3d"));
            manip.AddEffect("Rotate 90° clockwise", LucideIcons.Redo2, "Rotates the image 90 degrees clockwise.", () => Raise(Rotate90CWRequested));
            manip.AddEffect("Rotate 90° counter clockwise", LucideIcons.Undo2, "Rotates the image 90 degrees counter-clockwise.", () => Raise(Rotate90CCWRequested));
            manip.AddEffect("Rounded Corners...", LucideIcons.SquareRoundCorner, "Rounds the corners of the image.", () => RaiseDialog("rounded_corners"));
            manip.AddEffect("Scale...", LucideIcons.Scale, "Scales the image.", () => RaiseDialog("scale"));
            manip.AddEffect("Skew...", LucideIcons.MoveDiagonal, "Skews the image.", () => RaiseDialog("skew"));
            manip.AddEffect("Twirl...", LucideIcons.Orbit, "Applies a twirl effect.", () => RaiseDialog("twirl"));
            Categories.Add(manip);

            var adj = new EffectCategory("Adjustments");
            adj.AddEffect("Alpha...", LucideIcons.Droplet, "Adjusts the alpha transparency.", () => RaiseDialog("alpha"));
            adj.AddEffect("Auto contrast...", LucideIcons.Wand2, "Automatically adjusts the contrast.", () => RaiseDialog("auto_contrast"));
            adj.AddEffect("Black & White", LucideIcons.ShieldHalf, "Converts the image to black and white.", () => Raise(BlackAndWhiteRequested));
            adj.AddEffect("Brightness...", LucideIcons.SunMedium, "Adjusts image brightness.", () => RaiseDialog("brightness"));
            adj.AddEffect("Color matrix...", LucideIcons.TableProperties, "Applies a color matrix transformation.", () => RaiseDialog("color_matrix"));
            adj.AddEffect("Colorize...", LucideIcons.Palette, "Colorizes the image.", () => RaiseDialog("colorize"));
            adj.AddEffect("Contrast...", LucideIcons.Contrast, "Adjusts image contrast.", () => RaiseDialog("contrast"));
            adj.AddEffect("Duotone / Gradient map...", LucideIcons.Blend, "Maps grayscale tones to a custom multi-color gradient.", () => RaiseDialog("duotone_gradient_map"));
            adj.AddEffect("Exposure...", LucideIcons.Aperture, "Adjusts the exposure level.", () => RaiseDialog("exposure"));
            adj.AddEffect("Film emulation...", LucideIcons.Film, "Applies cinematic analog film looks with grain and fade.", () => RaiseDialog("film_emulation"));
            adj.AddEffect("Gamma...", LucideIcons.Gauge, "Adjusts the gamma level.", () => RaiseDialog("gamma"));
            adj.AddEffect("Grayscale...", LucideIcons.Moon, "Converts the image to grayscale.", () => RaiseDialog("grayscale"));
            adj.AddEffect("Hue...", LucideIcons.Pipette, "Adjusts the hue of the image.", () => RaiseDialog("hue"));
            adj.AddEffect("Invert", LucideIcons.RefreshCcwDot, "Inverts image colors.", () => Raise(InvertRequested));
            adj.AddEffect("Levels...", LucideIcons.SlidersVertical, "Adjusts image color levels.", () => RaiseDialog("levels"));
            adj.AddEffect("Polaroid", LucideIcons.Camera, "Applies a Polaroid effect.", () => Raise(PolaroidRequested));
            adj.AddEffect("Posterize...", LucideIcons.Layers3, "Reduces the number of colors to create a poster-like effect.", () => RaiseDialog("posterize"));
            adj.AddEffect("Replace Color...", LucideIcons.Replace, "Replaces a specific color.", () => RaiseDialog("replace_color"));
            adj.AddEffect("Saturation...", LucideIcons.Droplets, "Adjusts the color saturation.", () => RaiseDialog("saturation"));
            adj.AddEffect("Selective Color...", LucideIcons.SwatchBook, "Adjusts selective color channels.", () => RaiseDialog("selective_color"));
            adj.AddEffect("Sepia...", LucideIcons.Coffee, "Applies a sepia tone effect.", () => RaiseDialog("sepia"));
            adj.AddEffect("Shadows / Highlights...", LucideIcons.Lightbulb, "Adjusts shadows and highlights.", () => RaiseDialog("shadows_highlights"));
            adj.AddEffect("Solarize...", LucideIcons.Sun, "Applies a solarize effect.", () => RaiseDialog("solarize"));
            adj.AddEffect("Temperature / Tint...", LucideIcons.Thermometer, "Adjusts the color temperature and tint.", () => RaiseDialog("temperature_tint"));
            adj.AddEffect("Threshold...", LucideIcons.Binary, "Applies a contrast threshold.", () => RaiseDialog("threshold"));
            adj.AddEffect("Vibrance...", LucideIcons.Sparkles, "Adjusts the color vibrance.", () => RaiseDialog("vibrance"));
            Categories.Add(adj);

            var fil = new EffectCategory("Filters");
            fil.AddEffect("Edge detect", LucideIcons.ScanSearch, "Detects visible edges in the image.", () => Raise(EdgeDetectRequested));
            fil.AddEffect("Emboss", LucideIcons.Stamp, "Applies an emboss effect.", () => Raise(EmbossRequested));
            fil.AddEffect("Mean removal", LucideIcons.Sigma, "Removes the mean value from colors.", () => Raise(MeanRemovalRequested));
            fil.AddEffect("Smooth", LucideIcons.Waves, "Applies a smoothing effect.", () => Raise(SmoothRequested));
            fil.AddEffect("Wooden frame...", LucideIcons.Frame, "Expands the canvas with a beveled procedural wooden picture frame.", () => RaiseDialog("wooden_frame"), "wooden_frame");
            AddCatalogDrivenFilters(fil);
            Categories.Add(fil);

            var drawings = new EffectCategory("Drawings");
            drawings.AddEffect("Background...", LucideIcons.PaintBucket, "Fills transparent regions using a solid color or gradient background.", () => RaiseDialog("draw_background"));
            drawings.AddEffect("Background image...", LucideIcons.Image, "Draws an image behind the current canvas.", () => RaiseDialog("draw_background_image"));
            drawings.AddEffect("Checkerboard...", LucideIcons.Grid2X2Check, "Draws a checkerboard background behind the image.", () => RaiseDialog("draw_checkerboard"));
            drawings.AddEffect("Border...", LucideIcons.Frame, "Adds a border to the image.", () => RaiseDialog("border"));
            drawings.AddEffect("Image...", LucideIcons.ImagePlus, "Draws an image overlay with placement, sizing and opacity controls.", () => RaiseDialog("draw_image"));
            drawings.AddEffect("Line...", LucideIcons.Minus, "Draws a straight line overlay with start, end, color and thickness controls.", () => RaiseDialog("draw_line"));
            drawings.AddEffect("Particles...", LucideIcons.Sparkle, "Draws random particles from an image folder.", () => RaiseDialog("draw_particles"));
            drawings.AddEffect("Shape...", LucideIcons.VectorSquare, "Draws filled primitive shapes with placement, size and color controls.", () => RaiseDialog("draw_shape"));
            drawings.AddEffect("Text...", LucideIcons.TextCursor, "Draws stylized text with gradient, outline and shadow options.", () => RaiseDialog("draw_text"));
            drawings.AddEffect("Text watermark...", LucideIcons.Stamp, "Draws text inside a rounded watermark box with padding, border and optional shadow.", () => RaiseDialog("text_watermark"));
            Categories.Add(drawings);
        }

        private void AddCatalogDrivenFilters(EffectCategory category)
        {
            foreach (FilterDefinition definition in FilterCatalog.Definitions)
            {
                if (!definition.IncludeInFiltersCategory)
                {
                    continue;
                }

                category.AddEffect(
                    definition.BrowserLabel,
                    definition.Icon,
                    definition.Description,
                    () => RaiseDialog(definition.Id),
                    definition.Id);
            }
        }
    }
}
