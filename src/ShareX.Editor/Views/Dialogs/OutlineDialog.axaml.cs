using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ShareX.Editor.Helpers;
using SkiaSharp;

namespace ShareX.Editor.Views.Dialogs;

public partial class OutlineDialog : UserControl, IEffectDialog
{
    public event EventHandler<EffectEventArgs>? PreviewRequested;
    public event EventHandler<EffectEventArgs>? ApplyRequested;
    public event EventHandler? CancelRequested;

    private SKColor _color = SKColors.Black;
    private bool _isLoaded = false;

    // Control references
    private Slider? _sizeSlider;
    private Slider? _paddingSlider;
    private TextBox? _colorTextBox;
    private Border? _colorPreview;

    public OutlineDialog()
    {
        InitializeComponent();
        
        // Find controls after XAML is loaded
        _sizeSlider = this.FindControl<Slider>("SizeSlider");
        _paddingSlider = this.FindControl<Slider>("PaddingSlider");
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

    private int GetSize() => (int)(_sizeSlider?.Value ?? 3);
    private int GetPadding() => (int)(_paddingSlider?.Value ?? 0);

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
        var size = GetSize();
        var padding = GetPadding();
        
        PreviewRequested?.Invoke(this, new EffectEventArgs(
            img => ImageHelpers.ApplyOutline(img, size, padding, _color),
            "Outline applied"));
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        var size = GetSize();
        var padding = GetPadding();
        
        ApplyRequested?.Invoke(this, new EffectEventArgs(
            img => ImageHelpers.ApplyOutline(img, size, padding, _color),
            "Outline applied"));
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}
