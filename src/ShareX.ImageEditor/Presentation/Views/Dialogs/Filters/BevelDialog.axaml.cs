using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ShareX.ImageEditor.Core.ImageEffects.Filters;
using ShareX.ImageEditor.Presentation.Controls;
using SkiaSharp;

namespace ShareX.ImageEditor.Presentation.Views.Dialogs;

public partial class BevelDialog : UserControl, IEffectDialog
{
    public event EventHandler<EffectEventArgs>? PreviewRequested;
    public event EventHandler<EffectEventArgs>? ApplyRequested;
    public event EventHandler? CancelRequested;

    public BevelDialog()
    {
        AvaloniaXamlLoader.Load(this);
        SubscribeColorPicker("HighlightColorPicker");
        SubscribeColorPicker("ShadowColorPicker");
        AttachedToVisualTree += (s, e) => RequestPreview();
    }

    private void SubscribeColorPicker(string controlName)
    {
        ColorPickerDropdown? picker = this.FindControl<ColorPickerDropdown>(controlName);
        if (picker != null)
        {
            picker.PropertyChanged += OnColorPickerPropertyChanged;
        }
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

    private BevelImageEffect CreateEffect()
    {
        Avalonia.Media.Color highlight = this.FindControl<ColorPickerDropdown>("HighlightColorPicker")?.SelectedColorValue
            ?? Avalonia.Media.Color.Parse("#D9FFFFFF");
        Avalonia.Media.Color shadow = this.FindControl<ColorPickerDropdown>("ShadowColorPicker")?.SelectedColorValue
            ?? Avalonia.Media.Color.Parse("#B0000000");

        return new BevelImageEffect
        {
            Size = (int)Math.Round(GetValue("SizeSlider", 10d)),
            Strength = GetValue("StrengthSlider", 70d),
            LightAngle = GetValue("AngleSlider", 225d),
            HighlightColor = ToSkColor(highlight),
            ShadowColor = ToSkColor(shadow)
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
            "Bevel"));
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        ApplyRequested?.Invoke(this, new EffectEventArgs(
            img => CreateEffect().Apply(img),
            "Applied bevel"));
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}
