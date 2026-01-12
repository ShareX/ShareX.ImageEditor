using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ShareX.Editor.Helpers;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace ShareX.Editor.Views.Dialogs
{
    public partial class SelectiveColorDialog : UserControl
    {
        public event EventHandler<EffectEventArgs>? ApplyRequested;
        public event EventHandler<EffectEventArgs>? PreviewRequested;
        public event EventHandler? CancelRequested;

        private bool _suppressPreview = false;

        // Store values for each range in memory so users can adjust multiple ranges?
        // Usually Selective Color applies adjustments cumulatively or user switches tabs.
        // For simplicity: One range at a time? Or store state?
        // Standard behavior: The dialog builds a "recipe" of adjustments. 
        // But PreviewEffect usually takes ONE function that generates the bitmap from source.
        // If we want multiple ranges, we need to apply them sequentially in one pass.
        // To do that, we need to store the settings for ALL ranges.
        
        private Dictionary<ImageHelpers.SelectiveColorRange, (float h, float s, float l)> _adjustments = new();

        public SelectiveColorDialog()
        {
            AvaloniaXamlLoader.Load(this);
            
            // Initialize dictionary
            foreach (ImageHelpers.SelectiveColorRange r in Enum.GetValues(typeof(ImageHelpers.SelectiveColorRange)))
            {
                _adjustments[r] = (0, 0, 0);
            }
        }

        private void OnRangeChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_suppressPreview) return;
            
            // When range changes, update sliders to reflect stored values for that range
            var combo = this.FindControl<ComboBox>("RangeComboBox");
            if (combo.SelectedIndex >= 0)
            {
                var range = (ImageHelpers.SelectiveColorRange)combo.SelectedIndex;
                var (h, s, l) = _adjustments[range];

                _suppressPreview = true;
                this.FindControl<Slider>("HueSlider").Value = h;
                this.FindControl<Slider>("SatSlider").Value = s;
                this.FindControl<Slider>("LightSlider").Value = l;
                _suppressPreview = false;
            }
        }

        private void OnValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_suppressPreview) return;
            
            // Update stored value for current range
             var combo = this.FindControl<ComboBox>("RangeComboBox");
             if (combo != null && combo.SelectedIndex >= 0)
             {
                 var range = (ImageHelpers.SelectiveColorRange)combo.SelectedIndex;
                 float h = (float)(this.FindControl<Slider>("HueSlider")?.Value ?? 0);
                 float s = (float)(this.FindControl<Slider>("SatSlider")?.Value ?? 0);
                 float l = (float)(this.FindControl<Slider>("LightSlider")?.Value ?? 0);
                 
                 _adjustments[range] = (h, s, l);
                 
                 RequestPreview();
             }
        }

        private void RequestPreview()
        {
             PreviewRequested?.Invoke(this, new EffectEventArgs(img => ApplyAllAdjustments(img), "Selective Color Adjustment"));
        }
        
        private SKBitmap ApplyAllAdjustments(SKBitmap source)
        {
            // We need to apply ALL active adjustments.
            // Since ImageHelpers.ApplySelectiveColor takes ONE range, we might need a helper that takes all.
            // Or we iterate?
            // Iterating pixel by pixel and checking all ranges is efficient. 
            // Calling ApplySelectiveColor multiple times (9 times!) is very slow (9 passes).
            
            // We should create a new helper in ImageHelpers or make ApplySelectiveColor smarter?
            // Or just loop here? Code-behind logic isn't ideal for complex processing but...
            // Actually, let's just chain them for PREVIEW simplifiction? No, slow.
            
            // Better: Add a method to ImageHelpers that takes a Dictionary or array of adjustments.
            return Helpers.ImageHelpers.ApplySelectiveColorAdvanced(source, _adjustments);
        }

        private void OnApplyClick(object? sender, RoutedEventArgs e)
        {
            ApplyRequested?.Invoke(this, new EffectEventArgs(img => ApplyAllAdjustments(img), "Applied Selective Color"));
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
