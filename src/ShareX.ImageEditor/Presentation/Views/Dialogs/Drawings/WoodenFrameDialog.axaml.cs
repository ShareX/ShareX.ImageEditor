using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ShareX.ImageEditor.Core.ImageEffects.Drawings;
using ShareX.ImageEditor.Presentation.Controls;
using SkiaSharp;

namespace ShareX.ImageEditor.Presentation.Views.Dialogs;

public partial class WoodenFrameDialog : UserControl, IEffectDialog
{
    public event EventHandler<EffectEventArgs>? ApplyRequested;
    public event EventHandler<EffectEventArgs>? PreviewRequested;
    public event EventHandler? CancelRequested;

    private ColorPickerDropdown? _woodColorPicker;

    public WoodenFrameDialog()
    {
        AvaloniaXamlLoader.Load(this);

        _woodColorPicker = this.FindControl<ColorPickerDropdown>("WoodColorPicker");
        if (_woodColorPicker != null)
        {
            _woodColorPicker.PropertyChanged += OnColorPickerPropertyChanged;
        }

        AttachedToVisualTree += (s, e) => RequestPreview();
    }

    private void OnColorPickerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ColorPickerDropdown.SelectedColorValueProperty && IsLoaded)
        {
            RequestPreview();
        }
    }

    private float GetValue(string controlName, double fallback)
    {
        return (float)(this.FindControl<Slider>(controlName)?.Value ?? fallback);
    }

    private static SKColor ToSkColor(Avalonia.Media.Color color)
    {
        return new SKColor(color.R, color.G, color.B, color.A);
    }

    private WoodenFrameImageEffect CreateEffect()
    {
        Avalonia.Media.Color woodColor = _woodColorPicker?.SelectedColorValue ?? Avalonia.Media.Color.Parse("#FF8B5E3C");

        return new WoodenFrameImageEffect
        {
            FrameWidth = (int)Math.Round(GetValue("FrameWidthSlider", 48d)),
            GrainStrength = GetValue("GrainSlider", 60d),
            BevelStrength = GetValue("BevelSlider", 65d),
            WoodColor = ToSkColor(woodColor)
        };
    }

    private void OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (!IsLoaded) return;
        RequestPreview();
    }

    private void RequestPreview()
    {
        PreviewRequested?.Invoke(this, new EffectEventArgs(
            img => CreateEffect().Apply(img),
            "Wooden frame"));
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        ApplyRequested?.Invoke(this, new EffectEventArgs(
            img => CreateEffect().Apply(img),
            "Applied wooden frame"));
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}
