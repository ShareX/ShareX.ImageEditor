using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ShareX.Editor.Helpers;
using SkiaSharp;

namespace ShareX.Editor.Views.Dialogs;

public partial class BorderDialog : UserControl, IEffectDialog
{
    public event EventHandler<EffectEventArgs>? PreviewRequested;
    public event EventHandler<EffectEventArgs>? ApplyRequested;
    public event EventHandler? CancelRequested;

    private SKColor _color = SKColors.Black;
    private bool _isLoaded = false;

    // Control references
    private ComboBox? _typeComboBox;
    private ComboBox? _dashStyleComboBox;
    private Slider? _sizeSlider;
    private TextBox? _colorTextBox;
    private Border? _colorPreview;

    public BorderDialog()
    {
        InitializeComponent();
        
        // Find controls after XAML is loaded
        _typeComboBox = this.FindControl<ComboBox>("TypeComboBox");
        _dashStyleComboBox = this.FindControl<ComboBox>("DashStyleComboBox");
        _sizeSlider = this.FindControl<Slider>("SizeSlider");
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

    private ImageHelpers.BorderType GetBorderType()
    {
        return _typeComboBox?.SelectedIndex == 1 ? ImageHelpers.BorderType.Inside : ImageHelpers.BorderType.Outside;
    }

    private ImageHelpers.DashStyle GetDashStyle()
    {
        return _dashStyleComboBox?.SelectedIndex switch
        {
            1 => ImageHelpers.DashStyle.Dash,
            2 => ImageHelpers.DashStyle.Dot,
            3 => ImageHelpers.DashStyle.DashDot,
            _ => ImageHelpers.DashStyle.Solid
        };
    }

    private int GetSize() => (int)(_sizeSlider?.Value ?? 5);

    private void OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded) RaisePreview();
    }
    
    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
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
        var type = GetBorderType();
        var size = GetSize();
        var dashStyle = GetDashStyle();
        
        PreviewRequested?.Invoke(this, new EffectEventArgs(
            img => ImageHelpers.ApplyBorder(img, type, size, dashStyle, _color),
            "Border applied"));
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        var type = GetBorderType();
        var size = GetSize();
        var dashStyle = GetDashStyle();
        
        ApplyRequested?.Invoke(this, new EffectEventArgs(
            img => ImageHelpers.ApplyBorder(img, type, size, dashStyle, _color),
            "Border applied"));
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}
