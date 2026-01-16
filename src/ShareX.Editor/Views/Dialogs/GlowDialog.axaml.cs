using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ShareX.Editor.Helpers;
using SkiaSharp;

namespace ShareX.Editor.Views.Dialogs;

public partial class GlowDialog : UserControl, IEffectDialog
{
    public event EventHandler<EffectEventArgs>? PreviewRequested;
    public event EventHandler<EffectEventArgs>? ApplyRequested;
    public event EventHandler? CancelRequested;

    private SKColor _color = SKColors.Yellow;
    private bool _isLoaded = false;

    // Control references
    private Slider? _sizeSlider;
    private Slider? _strengthSlider;
    private Slider? _offsetXSlider;
    private Slider? _offsetYSlider;
    private TextBox? _colorTextBox;
    private Border? _colorPreview;

    public GlowDialog()
    {
        InitializeComponent();
        
        // Find controls after XAML is loaded
        _sizeSlider = this.FindControl<Slider>("SizeSlider");
        _strengthSlider = this.FindControl<Slider>("StrengthSlider");
        _offsetXSlider = this.FindControl<Slider>("OffsetXSlider");
        _offsetYSlider = this.FindControl<Slider>("OffsetYSlider");
        _colorTextBox = this.FindControl<TextBox>("ColorTextBox");
        _colorPreview = this.FindControl<Border>("ColorPreview");
        
        Loaded += OnLoaded;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        RaisePreview();
    }

    private int GetSize() => (int)(_sizeSlider?.Value ?? 20);
    private float GetStrength() => (float)(_strengthSlider?.Value ?? 80);
    private int GetOffsetX() => (int)(_offsetXSlider?.Value ?? 0);
    private int GetOffsetY() => (int)(_offsetYSlider?.Value ?? 0);

    private void OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded) RaisePreview();
    }

    private void OnColorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_colorTextBox != null && _colorPreview != null)
        {
            try
            {
                var color = Color.Parse(_colorTextBox.Text ?? "#FFFF00");
                _colorPreview.Background = new SolidColorBrush(color);
                _color = new SKColor(color.R, color.G, color.B, color.A);
                if (_isLoaded) RaisePreview();
            }
            catch { }
        }
    }

    private void RaisePreview()
    {
        PreviewRequested?.Invoke(this, new EffectEventArgs(
            img => ImageHelpers.ApplyGlow(img, GetSize(), GetStrength(), _color, GetOffsetX(), GetOffsetY()),
            "Glow applied"));
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        ApplyRequested?.Invoke(this, new EffectEventArgs(
            img => ImageHelpers.ApplyGlow(img, GetSize(), GetStrength(), _color, GetOffsetX(), GetOffsetY()),
            "Glow applied"));
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}
