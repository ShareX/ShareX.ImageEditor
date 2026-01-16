using ShareX.Editor.ImageEffects.Manipulations;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ShareX.Editor.Helpers;
using ShareX.Editor.ImageEffects;
using System;

namespace ShareX.Editor.Views.Dialogs
{
    public partial class SkewDialog : UserControl, IEffectDialog
    {
        public event EventHandler<EffectEventArgs>? ApplyRequested;
        public event EventHandler<EffectEventArgs>? PreviewRequested;
        public event EventHandler? CancelRequested;

        public SkewDialog()
        {
            AvaloniaXamlLoader.Load(this);
            this.AttachedToVisualTree += (s, e) => RequestPreview();
        }

        private void OnValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (!this.IsLoaded) return;
            RequestPreview();
        }

        private void RequestPreview()
        {
            int horizontal = (int)(this.FindControl<Slider>("HorizontalSlider")?.Value ?? 0);
            int vertical = (int)(this.FindControl<Slider>("VerticalSlider")?.Value ?? 0);
            PreviewRequested?.Invoke(this, new EffectEventArgs(img => new SkewImageEffect { Horizontally = horizontal, Vertically = vertical }.Apply(img), "Skew"));
        }

        private void OnApplyClick(object? sender, RoutedEventArgs e)
        {
            int horizontal = (int)(this.FindControl<Slider>("HorizontalSlider")?.Value ?? 0);
            int vertical = (int)(this.FindControl<Slider>("VerticalSlider")?.Value ?? 0);
            ApplyRequested?.Invoke(this, new EffectEventArgs(img => new SkewImageEffect { Horizontally = horizontal, Vertically = vertical }.Apply(img), "Applied Skew"));
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}

