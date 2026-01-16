using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;

namespace ShareX.Editor.Controls
{
    public partial class EffectsMenuDropdown : UserControl
    {
        // Events for menu actions
        public event EventHandler? BrightnessRequested;
        public event EventHandler? ContrastRequested;
        public event EventHandler? HueRequested;
        public event EventHandler? SaturationRequested;
        public event EventHandler? GammaRequested;
        public event EventHandler? AlphaRequested;
        public event EventHandler? InvertRequested;
        public event EventHandler? BlackAndWhiteRequested;
        public event EventHandler? SepiaRequested;
        public event EventHandler? PolaroidRequested;
        public event EventHandler? ColorizeRequested;
        public event EventHandler? SelectiveColorRequested;
        public event EventHandler? ReplaceColorRequested;
        public event EventHandler? GrayscaleRequested;

        // Migrated from EditMenuDropdown
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

        public event EventHandler? RoundedCornersRequested;
        public event EventHandler? SkewRequested;

        // Filters
        public event EventHandler? BorderRequested;
        public event EventHandler? OutlineRequested;
        public event EventHandler? ShadowRequested;
        public event EventHandler? GlowRequested;
        public event EventHandler? ReflectionRequested;
        public event EventHandler? TornEdgeRequested;
        public event EventHandler? SliceRequested;

        public event EventHandler? BlurRequested;
        public event EventHandler? PixelateRequested;
        public event EventHandler? SharpenRequested;

        public EffectsMenuDropdown()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void Raise(EventHandler? handler)
        {
            Dispatcher.UIThread.Post(() => handler?.Invoke(this, EventArgs.Empty));
        }

        // --- Adjustments ---

        private void OnBrightnessClick(object? sender, RoutedEventArgs e) => Raise(BrightnessRequested);
        private void OnContrastClick(object? sender, RoutedEventArgs e) => Raise(ContrastRequested);
        private void OnHueClick(object? sender, RoutedEventArgs e) => Raise(HueRequested);
        private void OnSaturationClick(object? sender, RoutedEventArgs e) => Raise(SaturationRequested);
        private void OnGammaClick(object? sender, RoutedEventArgs e) => Raise(GammaRequested);
        private void OnAlphaClick(object? sender, RoutedEventArgs e) => Raise(AlphaRequested);
        private void OnInvertClick(object? sender, RoutedEventArgs e) => Raise(InvertRequested);
        private void OnBlackAndWhiteClick(object? sender, RoutedEventArgs e) => Raise(BlackAndWhiteRequested);
        private void OnSepiaClick(object? sender, RoutedEventArgs e) => Raise(SepiaRequested);
        private void OnPolaroidClick(object? sender, RoutedEventArgs e) => Raise(PolaroidRequested);
        private void OnColorizeClick(object? sender, RoutedEventArgs e) => Raise(ColorizeRequested);
        private void OnSelectiveColorClick(object? sender, RoutedEventArgs e) => Raise(SelectiveColorRequested);
        private void OnReplaceColorClick(object? sender, RoutedEventArgs e) => Raise(ReplaceColorRequested);
        private void OnGrayscaleClick(object? sender, RoutedEventArgs e) => Raise(GrayscaleRequested);

        // --- Manipulations ---

        private void OnResizeImageClick(object? sender, RoutedEventArgs e) => Raise(ResizeImageRequested);
        private void OnResizeCanvasClick(object? sender, RoutedEventArgs e) => Raise(ResizeCanvasRequested);
        private void OnCropImageClick(object? sender, RoutedEventArgs e) => Raise(CropImageRequested);
        private void OnAutoCropImageClick(object? sender, RoutedEventArgs e) => Raise(AutoCropImageRequested);
        private void OnRotate90CWClick(object? sender, RoutedEventArgs e) => Raise(Rotate90CWRequested);
        private void OnRotate90CCWClick(object? sender, RoutedEventArgs e) => Raise(Rotate90CCWRequested);
        private void OnRotate180Click(object? sender, RoutedEventArgs e) => Raise(Rotate180Requested);
        private void OnRotateCustomAngleClick(object? sender, RoutedEventArgs e) => Raise(RotateCustomAngleRequested);
        private void OnFlipHorizontalClick(object? sender, RoutedEventArgs e) => Raise(FlipHorizontalRequested);
        private void OnFlipVerticalClick(object? sender, RoutedEventArgs e) => Raise(FlipVerticalRequested);

        private void OnRoundedCornersClick(object? sender, RoutedEventArgs e) => Raise(RoundedCornersRequested);
        private void OnSkewClick(object? sender, RoutedEventArgs e) => Raise(SkewRequested);

        // --- Filters ---

        private void OnBorderClick(object? sender, RoutedEventArgs e) => Raise(BorderRequested);
        private void OnOutlineClick(object? sender, RoutedEventArgs e) => Raise(OutlineRequested);
        private void OnShadowClick(object? sender, RoutedEventArgs e) => Raise(ShadowRequested);
        private void OnGlowClick(object? sender, RoutedEventArgs e) => Raise(GlowRequested);
        private void OnReflectionClick(object? sender, RoutedEventArgs e) => Raise(ReflectionRequested);
        private void OnTornEdgeClick(object? sender, RoutedEventArgs e) => Raise(TornEdgeRequested);
        private void OnSliceClick(object? sender, RoutedEventArgs e) => Raise(SliceRequested);
        private void OnBlurClick(object? sender, RoutedEventArgs e) => Raise(BlurRequested);
        private void OnPixelateClick(object? sender, RoutedEventArgs e) => Raise(PixelateRequested);
        private void OnSharpenClick(object? sender, RoutedEventArgs e) => Raise(SharpenRequested);
    }
}
