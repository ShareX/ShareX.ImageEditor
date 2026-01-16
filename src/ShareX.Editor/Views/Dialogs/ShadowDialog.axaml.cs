using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ShareX.Editor.Helpers;
using SkiaSharp;

namespace ShareX.Editor.Views.Dialogs;

public partial class ShadowDialog : UserControl, IEffectDialog
{
    public event EventHandler<EffectEventArgs>? PreviewRequested;
    public event EventHandler<EffectEventArgs>? ApplyRequested;
    public event EventHandler? CancelRequested;

    private SKColor _color = SKColors.Black;
    private bool _isLoaded = false;

    // Control references
    private Slider? _opacitySlider;
    private Slider? _sizeSlider;
    private Slider? _darknessSlider;
    private Slider? _offsetXSlider;
    private Slider? _offsetYSlider;
    private CheckBox? _autoResizeCheckBox;
    private TextBox? _colorTextBox;
    private Border? _colorPreview;

    public ShadowDialog()
    {
        InitializeComponent();
        
        // Find controls after XAML is loaded
        _opacitySlider = this.FindControl<Slider>("OpacitySlider");
        _sizeSlider = this.FindControl<Slider>("SizeSlider");
        _darknessSlider = this.FindControl<Slider>("DarknessSlider");
        _offsetXSlider = this.FindControl<Slider>("OffsetXSlider");
        _offsetYSlider = this.FindControl<Slider>("OffsetYSlider");
        _autoResizeCheckBox = this.FindControl<CheckBox>("AutoResizeCheckBox");
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

    private float GetOpacity() => (float)(_opacitySlider?.Value ?? 80);
    private int GetSize() => (int)(_sizeSlider?.Value ?? 20);
    private float GetDarkness() => (float)(_darknessSlider?.Value ?? 50) / 100f;
    private int GetOffsetX() => (int)(_offsetXSlider?.Value ?? 5);
    private int GetOffsetY() => (int)(_offsetYSlider?.Value ?? 5);
    private bool GetAutoResize() => _autoResizeCheckBox?.IsChecked ?? true;

    private void OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded) RaisePreview();
    }

    private void OnCheckChanged(object? sender, RoutedEventArgs e)
    {
        if (_isLoaded) RaisePreview();
    }

    private void OnColorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_colorTextBox != null && _colorPreview != null)
        {
            try
            {
                var color = Color.Parse(_colorTextBox.Text ?? "#000000");
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
            img => ImageHelpers.ApplyShadow(img, GetOpacity(), GetSize(), GetDarkness(), _color, GetOffsetX(), GetOffsetY(), GetAutoResize()),
            "Shadow applied"));
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        ApplyRequested?.Invoke(this, new EffectEventArgs(
            img => ImageHelpers.ApplyShadow(img, GetOpacity(), GetSize(), GetDarkness(), _color, GetOffsetX(), GetOffsetY(), GetAutoResize()),
            "Shadow applied"));
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}
