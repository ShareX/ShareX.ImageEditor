using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace ShareX.ImageEditor.Controls
{
    /// <summary>
    /// XIP0039 Pain Point 3: Carries the effect identifier for
    /// <see cref="EffectsMenuDropdown.EffectDialogRequested"/>.
    /// </summary>
    public sealed class EffectDialogRequestedEventArgs : EventArgs
    {
        /// <summary>Registry key understood by <c>EffectDialogRegistry.TryCreate</c>.</summary>
        public string EffectId { get; }

        public EffectDialogRequestedEventArgs(string effectId) => EffectId = effectId;
    }

    public partial class EffectsMenuDropdown : UserControl
    {
        // XIP0039 Pain Point 3: Single aggregate event for all dialog-based effects.
        // New effects need only a registry entry + a menu click that calls RaiseDialog("id").
        // No new handler method in EditorView is required.
        public event EventHandler<EffectDialogRequestedEventArgs>? EffectDialogRequested;

        // --- Non-dialog events (immediate VM commands or special-initialisation dialogs) ---

        public event EventHandler? InvertRequested;
        public event EventHandler? BlackAndWhiteRequested;
        public event EventHandler? PolaroidRequested;

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

        public EffectsMenuDropdown()
        {
            AvaloniaXamlLoader.Load(this);
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

        // --- Adjustments (dialog effects → aggregate event) ---

        private void OnBrightnessClick(object? sender, RoutedEventArgs e)      => RaiseDialog("brightness");
        private void OnContrastClick(object? sender, RoutedEventArgs e)         => RaiseDialog("contrast");
        private void OnHueClick(object? sender, RoutedEventArgs e)              => RaiseDialog("hue");
        private void OnSaturationClick(object? sender, RoutedEventArgs e)       => RaiseDialog("saturation");
        private void OnGammaClick(object? sender, RoutedEventArgs e)            => RaiseDialog("gamma");
        private void OnAlphaClick(object? sender, RoutedEventArgs e)            => RaiseDialog("alpha");
        private void OnColorizeClick(object? sender, RoutedEventArgs e)         => RaiseDialog("colorize");
        private void OnSelectiveColorClick(object? sender, RoutedEventArgs e)   => RaiseDialog("selective_color");
        private void OnReplaceColorClick(object? sender, RoutedEventArgs e)     => RaiseDialog("replace_color");
        private void OnGrayscaleClick(object? sender, RoutedEventArgs e)        => RaiseDialog("grayscale");
        private void OnSepiaClick(object? sender, RoutedEventArgs e)            => RaiseDialog("sepia");

        // --- Immediate adjustments (no dialog) ---

        private void OnInvertClick(object? sender, RoutedEventArgs e)           => Raise(InvertRequested);
        private void OnBlackAndWhiteClick(object? sender, RoutedEventArgs e)    => Raise(BlackAndWhiteRequested);
        private void OnPolaroidClick(object? sender, RoutedEventArgs e)         => Raise(PolaroidRequested);

        // --- Manipulations ---

        private void OnResizeImageClick(object? sender, RoutedEventArgs e)      => Raise(ResizeImageRequested);
        private void OnResizeCanvasClick(object? sender, RoutedEventArgs e)     => Raise(ResizeCanvasRequested);
        private void OnCropImageClick(object? sender, RoutedEventArgs e)        => Raise(CropImageRequested);
        private void OnAutoCropImageClick(object? sender, RoutedEventArgs e)    => Raise(AutoCropImageRequested);
        private void OnRotate90CWClick(object? sender, RoutedEventArgs e)       => Raise(Rotate90CWRequested);
        private void OnRotate90CCWClick(object? sender, RoutedEventArgs e)      => Raise(Rotate90CCWRequested);
        private void OnRotate180Click(object? sender, RoutedEventArgs e)        => Raise(Rotate180Requested);
        private void OnRotateCustomAngleClick(object? sender, RoutedEventArgs e) => Raise(RotateCustomAngleRequested);
        private void OnFlipHorizontalClick(object? sender, RoutedEventArgs e)   => Raise(FlipHorizontalRequested);
        private void OnFlipVerticalClick(object? sender, RoutedEventArgs e)     => Raise(FlipVerticalRequested);

        // --- Transforms (dialog effects → aggregate event) ---

        private void OnRoundedCornersClick(object? sender, RoutedEventArgs e)   => RaiseDialog("rounded_corners");
        private void OnSkewClick(object? sender, RoutedEventArgs e)             => RaiseDialog("skew");
        private void OnRotate3DClick(object? sender, RoutedEventArgs e)         => RaiseDialog("rotate_3d");
        private void OnRotate3DBoxClick(object? sender, RoutedEventArgs e)      => RaiseDialog("rotate_3d_box");

        // --- Filters (dialog effects → aggregate event) ---

        private void OnBorderClick(object? sender, RoutedEventArgs e)           => RaiseDialog("border");
        private void OnOutlineClick(object? sender, RoutedEventArgs e)          => RaiseDialog("outline");
        private void OnShadowClick(object? sender, RoutedEventArgs e)           => RaiseDialog("shadow");
        private void OnGlowClick(object? sender, RoutedEventArgs e)             => RaiseDialog("glow");
        private void OnReflectionClick(object? sender, RoutedEventArgs e)       => RaiseDialog("reflection");
        private void OnTornEdgeClick(object? sender, RoutedEventArgs e)         => RaiseDialog("torn_edge");
        private void OnSliceClick(object? sender, RoutedEventArgs e)            => RaiseDialog("slice");

        // --- Quality (dialog effects → aggregate event) ---

        private void OnBlurClick(object? sender, RoutedEventArgs e)             => RaiseDialog("blur");
        private void OnPixelateClick(object? sender, RoutedEventArgs e)         => RaiseDialog("pixelate");
        private void OnSharpenClick(object? sender, RoutedEventArgs e)          => RaiseDialog("sharpen");
    }
}
