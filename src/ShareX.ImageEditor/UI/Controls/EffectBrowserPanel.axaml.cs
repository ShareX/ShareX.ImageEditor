using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;

namespace ShareX.ImageEditor.Controls
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

        public ObservableCollection<EffectItem> AllEffects { get; } = new();

        [ObservableProperty]
        private ObservableCollection<EffectItem> _visibleEffects = new();

        [ObservableProperty]
        private bool _isVisible = true;

        public EffectCategory(string name)
        {
            Name = name;
        }

        public void AddEffect(string name, string icon, string description, Action execute)
        {
            var effect = new EffectItem(name, icon, description, execute);
            AllEffects.Add(effect);
            VisibleEffects.Add(effect);
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
            IsVisible = VisibleEffects.Count > 0;
        }
    }

    public partial class EffectItem : ObservableObject
    {
        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _icon;

        [ObservableProperty]
        private string _description;

        public Action ExecuteAction { get; }

        public EffectItem(string name, string icon, string description, Action executeAction)
        {
            Name = name;
            Icon = icon;
            Description = description;
            ExecuteAction = executeAction;
        }

        [RelayCommand]
        private void Execute()
        {
            ExecuteAction?.Invoke();
        }
    }

    public partial class EffectBrowserPanel : UserControl
    {
        public event EventHandler<EffectDialogRequestedEventArgs>? EffectDialogRequested;

        public event EventHandler? InvertRequested;
        public event EventHandler? BlackAndWhiteRequested;
        public event EventHandler? PolaroidRequested;
        public event EventHandler? EdgeDetectRequested;
        public event EventHandler? EmbossRequested;
        public event EventHandler? MeanRemovalRequested;
        public event EventHandler? SmoothRequested;

        public event EventHandler? ResizeImageRequested;
        public event EventHandler? ResizeCanvasRequested;
        public event EventHandler? CropImageRequested;
        public event EventHandler? AutoCropImageRequested;
        public event EventHandler? Rotate90CWRequested;
        public event EventHandler? Rotate90CCWRequested;
        public event EventHandler? Rotate180Requested;
        public event EventHandler? RotateCustomAngleRequested;
        public event EventHandler? FlipHorizontalRequested;
        public event EventHandler? FlipVerticalRequested;

        public ObservableCollection<EffectCategory> Categories { get; } = new();

        public EffectBrowserPanel()
        {
            AvaloniaXamlLoader.Load(this);
            InitializeEffects();
            
            var categoriesControl = this.FindControl<ItemsControl>("CategoriesControl");
            if (categoriesControl != null)
            {
                categoriesControl.ItemsSource = Categories;
            }
        }

        private void OnSearchTextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
        {
            var searchBox = sender as TextBox;
            string searchText = searchBox?.Text ?? string.Empty;
            foreach (var category in Categories)
            {
                category.Filter(searchText);
            }
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

        private void InitializeEffects()
        {
            var manip = new EffectCategory("Manipulations");
            manip.AddEffect("3D Box / Extrude...", "\uf1b3", "Applies a 3D box or extrude effect.", () => RaiseDialog("rotate_3d_box"));
            manip.AddEffect("Auto crop image", "\uf0d0", "Automatically crops the image by finding its edges.", () => Raise(AutoCropImageRequested));
            manip.AddEffect("Crop image...", "\uf565", "Crops the image.", () => Raise(CropImageRequested));
            manip.AddEffect("Displacement map...", "\uf0c2", "Applies a displacement map.", () => RaiseDialog("displacement_map"));
            manip.AddEffect("Flip...", "\uf00a", "Flip the image.", () => RaiseDialog("flip"));
            manip.AddEffect("Flip horizontal", "\uf337", "Flips the image horizontally.", () => Raise(FlipHorizontalRequested));
            manip.AddEffect("Flip vertical", "\uf338", "Flips the image vertically.", () => Raise(FlipVerticalRequested));
            manip.AddEffect("Perspective warp...", "\uf53f", "Warps the image perspective.", () => RaiseDialog("perspective_warp"));
            manip.AddEffect("Pinch / bulge...", "\uf1fb", "Applies a pinch or bulge effect.", () => RaiseDialog("pinch_bulge"));
            manip.AddEffect("Resize canvas...", "\uf853", "Resizes the canvas.", () => Raise(ResizeCanvasRequested));
            manip.AddEffect("Resize image...", "\uf31e", "Resizes the image.", () => Raise(ResizeImageRequested));
            manip.AddEffect("Rotate...", "\uf1de", "Rotates the image by a custom angle.", () => Raise(RotateCustomAngleRequested));
            manip.AddEffect("Rotate 180°", "\uf021", "Rotates the image by 180 degrees.", () => Raise(Rotate180Requested));
            manip.AddEffect("Rotate 3D...", "\uf1b2", "Rotates the image in 3D space.", () => RaiseDialog("rotate_3d"));
            manip.AddEffect("Rotate 90° clockwise", "\uf01e", "Rotates the image 90 degrees clockwise.", () => Raise(Rotate90CWRequested));
            manip.AddEffect("Rotate 90° counter clockwise", "\uf0e2", "Rotates the image 90 degrees counter-clockwise.", () => Raise(Rotate90CCWRequested));
            manip.AddEffect("Rounded Corners...", "\uf042", "Rounds the corners of the image.", () => RaiseDialog("rounded_corners"));
            manip.AddEffect("Scale...", "\uf185", "Scales the image.", () => RaiseDialog("scale"));
            manip.AddEffect("Skew...", "\uf2ea", "Skews the image.", () => RaiseDialog("skew"));
            manip.AddEffect("Twirl...", "\uf043", "Applies a twirl effect.", () => RaiseDialog("twirl"));
            Categories.Add(manip);

            var adj = new EffectCategory("Adjustments");
            adj.AddEffect("Alpha...", "\uf1fb", "Adjusts the alpha transparency.", () => RaiseDialog("alpha"));
            adj.AddEffect("Auto contrast...", "\uf042", "Automatically adjusts the contrast.", () => RaiseDialog("auto_contrast"));
            adj.AddEffect("Black & White", "\uf1b2", "Converts the image to black and white.", () => Raise(BlackAndWhiteRequested));
            adj.AddEffect("Brightness...", "\uf185", "Adjusts image brightness.", () => RaiseDialog("brightness"));
            adj.AddEffect("Color matrix...", "\uf00a", "Applies a color matrix transformation.", () => RaiseDialog("color_matrix"));
            adj.AddEffect("Colorize...", "\uf043", "Colorizes the image.", () => RaiseDialog("colorize"));
            adj.AddEffect("Contrast...", "\uf042", "Adjusts image contrast.", () => RaiseDialog("contrast"));
            adj.AddEffect("Exposure...", "\uf01e", "Adjusts the exposure level.", () => RaiseDialog("exposure"));
            adj.AddEffect("Gamma...", "\uf2ea", "Adjusts the gamma level.", () => RaiseDialog("gamma"));
            adj.AddEffect("Grayscale...", "\uf0c2", "Converts the image to grayscale.", () => RaiseDialog("grayscale"));
            adj.AddEffect("Hue...", "\uf53f", "Adjusts the hue of the image.", () => RaiseDialog("hue"));
            adj.AddEffect("Invert", "\uf362", "Inverts image colors.", () => Raise(InvertRequested));
            adj.AddEffect("Levels...", "\uf853", "Adjusts image color levels.", () => RaiseDialog("levels"));
            adj.AddEffect("Polaroid", "\uf03e", "Applies a Polaroid effect.", () => Raise(PolaroidRequested));
            adj.AddEffect("Posterize...", "\uf00a", "Reduces the number of colors to create a poster-like effect.", () => RaiseDialog("posterize"));
            adj.AddEffect("Replace Color...", "\uf021", "Replaces a specific color.", () => RaiseDialog("replace_color"));
            adj.AddEffect("Saturation...", "\uf591", "Adjusts the color saturation.", () => RaiseDialog("saturation"));
            adj.AddEffect("Selective Color...", "\uf0d0", "Adjusts selective color channels.", () => RaiseDialog("selective_color"));
            adj.AddEffect("Sepia...", "\uf0f4", "Applies a sepia tone effect.", () => RaiseDialog("sepia"));
            adj.AddEffect("Shadows / Highlights...", "\uf338", "Adjusts shadows and highlights.", () => RaiseDialog("shadows_highlights"));
            adj.AddEffect("Solarize...", "\uf185", "Applies a solarize effect.", () => RaiseDialog("solarize"));
            adj.AddEffect("Temperature / Tint...", "\uf337", "Adjusts the color temperature and tint.", () => RaiseDialog("temperature_tint"));
            adj.AddEffect("Threshold...", "\uf591", "Applies a contrast threshold.", () => RaiseDialog("threshold"));
            adj.AddEffect("Vibrance...", "\uf043", "Adjusts the color vibrance.", () => RaiseDialog("vibrance"));
            Categories.Add(adj);

            var fil = new EffectCategory("Filters");
            fil.AddEffect("Add noise...", "\uf0c2", "Adds noise to the image.", () => RaiseDialog("add_noise"));
            fil.AddEffect("Blur...", "\uf0c2", "Applies a blur effect.", () => RaiseDialog("blur"));
            fil.AddEffect("Border...", "\uf565", "Adds a border to the image.", () => RaiseDialog("border"));
            fil.AddEffect("Color depth...", "\uf53f", "Changes the color depth of the image.", () => RaiseDialog("color_depth"));
            fil.AddEffect("Convolution matrix...", "\uf1de", "Applies a custom convolution matrix.", () => RaiseDialog("convolution_matrix"));
            fil.AddEffect("Edge detect", "\uf591", "Detects visible edges in the image.", () => Raise(EdgeDetectRequested));
            fil.AddEffect("Emboss", "\uf1b2", "Applies an emboss effect.", () => Raise(EmbossRequested));
            fil.AddEffect("Gaussian blur...", "\uf185", "Applies a Gaussian blur effect.", () => RaiseDialog("gaussian_blur"));
            fil.AddEffect("Glow...", "\uf043", "Applies a glowing effect.", () => RaiseDialog("glow"));
            fil.AddEffect("Mean removal", "\uf853", "Removes the mean value from colors.", () => Raise(MeanRemovalRequested));
            fil.AddEffect("Median filter...", "\uf00a", "Applies a median filter for noise reduction.", () => RaiseDialog("median_filter"));
            fil.AddEffect("Motion blur...", "\uf01e", "Applies a motion blur effect.", () => RaiseDialog("motion_blur"));
            fil.AddEffect("Oil paint...", "\uf53f", "Makes the image look like an oil painting.", () => RaiseDialog("oil_paint"));
            fil.AddEffect("Outline...", "\uf042", "Applies an outline effect.", () => RaiseDialog("outline"));
            fil.AddEffect("Pixelate...", "\uf1b3", "Pixelates the image.", () => RaiseDialog("pixelate"));
            fil.AddEffect("Reflection...", "\uf338", "Adds a reflection to the bottom of the image.", () => RaiseDialog("reflection"));
            fil.AddEffect("RGB split...", "\uf03e", "Splits the red, green, and blue color channels.", () => RaiseDialog("rgb_split"));
            fil.AddEffect("Shadow...", "\uf1fb", "Adds a drop shadow to the image.", () => RaiseDialog("shadow"));
            fil.AddEffect("Sharpen...", "\uf0d0", "Sharpens the image.", () => RaiseDialog("sharpen"));
            fil.AddEffect("Slice...", "\uf337", "Slices the image.", () => RaiseDialog("slice"));
            fil.AddEffect("Sobel edge...", "\uf591", "Applies a Sobel edge detection filter.", () => RaiseDialog("sobel_edge"));
            fil.AddEffect("Smooth", "\uf0f4", "Applies a smoothing effect.", () => Raise(SmoothRequested));
            fil.AddEffect("Torn edge...", "\uf021", "Adds a torn edge border effect.", () => RaiseDialog("torn_edge"));
            fil.AddEffect("Unsharp mask...", "\uf0d0", "Applies an unsharp mask filter.", () => RaiseDialog("unsharp_mask"));
            fil.AddEffect("Vignette...", "\uf1fb", "Applies a vignette effect.", () => RaiseDialog("vignette"));
            fil.AddEffect("Wave edge...", "\uf2ea", "Adds a wavy edge to the image.", () => RaiseDialog("wave_edge"));
            Categories.Add(fil);
        }
    }
}
