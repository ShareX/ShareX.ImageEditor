using ShareX.Editor.ImageEffects.Adjustments;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ShareX.Editor.ImageEffects;
using SkiaSharp;
using System;

namespace ShareX.Editor.Views.Dialogs
{
    public partial class ReplaceColorDialog : UserControl, IEffectDialog
    {
        public event EventHandler<EffectEventArgs>? ApplyRequested;
        public event EventHandler<EffectEventArgs>? PreviewRequested;
        public event EventHandler? CancelRequested;

        private bool _suppressPreview = false;

        public ReplaceColorDialog()
        {
            AvaloniaXamlLoader.Load(this);
            
            var t1 = this.FindControl<TextBox>("TargetColorHex");
            if (t1 != null) t1.PropertyChanged += OnColorTextChanged;
            
            var t2 = this.FindControl<TextBox>("ReplaceColorHex");
            if (t2 != null) t2.PropertyChanged += OnColorTextChanged;
            
            UpdateColorPreviews();
            // We'll request preview once loaded to ensure VM is ready
            this.AttachedToVisualTree += (s, e) => RequestPreview();
        }

        private void OnColorTextChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == TextBox.TextProperty)
            {
                UpdateColorPreviews();
                if (!_suppressPreview && this.IsLoaded) RequestPreview();
            }
        }

        private void OnValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_suppressPreview || !this.IsLoaded) return;
            RequestPreview();
        }

        private void UpdateColorPreviews()
        {
             UpdateBrush("TargetColorHex", "TargetColorPreview");
             UpdateBrush("ReplaceColorHex", "ReplaceColorPreview");
        }

        private void UpdateBrush(string textBoxName, string borderName)
        {
            var txt = this.FindControl<TextBox>(textBoxName)?.Text;
            var border = this.FindControl<Border>(borderName);
            if (border != null && !string.IsNullOrEmpty(txt))
            {
                if (Color.TryParse(txt, out Color c))
                {
                    border.Background = new SolidColorBrush(c);
                }
            }
        }

        private void RequestPreview()
        {
             if (!this.IsLoaded) return;

             SKColor target = ParseColor(this.FindControl<TextBox>("TargetColorHex")?.Text);
             SKColor replace = ParseColor(this.FindControl<TextBox>("ReplaceColorHex")?.Text);
             float tolerance = (float)(this.FindControl<Slider>("ToleranceSlider")?.Value ?? 0);

             PreviewRequested?.Invoke(this, new EffectEventArgs(img => new ReplaceColorImageEffect { TargetColor = target, ReplaceColor = replace, Tolerance = tolerance }.Apply(img), $"Replace Color"));
        }
        
        private SKColor ParseColor(string? hex)
        {
            if (string.IsNullOrEmpty(hex)) return SKColors.Transparent;
            try
            {
                 if (Color.TryParse(hex, out Color c))
                 {
                     return new SKColor(c.R, c.G, c.B, c.A);
                 }
            }
            catch {}
            return SKColors.Transparent;
        }

        private void OnApplyClick(object? sender, RoutedEventArgs e)
        {
             SKColor target = ParseColor(this.FindControl<TextBox>("TargetColorHex")?.Text);
             SKColor replace = ParseColor(this.FindControl<TextBox>("ReplaceColorHex")?.Text);
             float tolerance = (float)(this.FindControl<Slider>("ToleranceSlider")?.Value ?? 0);
            
            ApplyRequested?.Invoke(this, new EffectEventArgs(img => new ReplaceColorImageEffect { TargetColor = target, ReplaceColor = replace, Tolerance = tolerance }.Apply(img), "Applied Replace Color"));
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}

