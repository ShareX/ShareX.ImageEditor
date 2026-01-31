using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareX.ImageEditor.Annotations;
using ShareX.ImageEditor.Extensions;
using System.Collections.ObjectModel;

namespace ShareX.ImageEditor.ViewModels;

/// <summary>
/// Lightweight ViewModel that drives the editor view without depending on the host application.
/// </summary>
public partial class EditorViewModel : ObservableObject
{
    public sealed class GradientPreset
    {
        public required string Name { get; init; }
        public required IBrush Brush { get; init; }
    }

    private readonly EditorOptions _options;
    public EditorOptions Options => _options;

    private const double MinZoom = 0.25;
    private const double MaxZoom = 4.0;
    private const double ZoomStep = 0.1;
    private const string OutputRatioAuto = "Auto";

    public static readonly string[] ColorPalette =
    [
        "#EF4444", "#F59E0B", "#84CC16", "#06B6D4",
        "#3B82F6", "#8B5CF6", "#EC4899", "#F97316",
        "#14B8A6", "#4B5563", "#FFFFFF", "#000000"
    ];

    public static readonly int[] StrokeWidths = [2, 4, 6, 8, 10];

    public ObservableCollection<GradientPreset> GradientPresets { get; }

    public event EventHandler? UndoRequested;
    public event EventHandler? RedoRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? ClearAnnotationsRequested;
    public event EventHandler? CopyRequested;
    public event EventHandler? SaveRequested;
    public event EventHandler? SaveAsRequested;
    public event EventHandler<CropEventArgs>? CropRequested;
    public event EventHandler? ApplyEffectRequested;

    public class CropEventArgs : EventArgs
    {
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }

        public CropEventArgs(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    private Bitmap? _previewImage;
    public Bitmap? PreviewImage
    {
        get => _previewImage;
        set
        {
            if (SetProperty(ref _previewImage, value))
            {
                OnPreviewImageChanged(value);
            }
        }
    }

    private bool _hasPreviewImage;
    public bool HasPreviewImage
    {
        get => _hasPreviewImage;
        set => SetProperty(ref _hasPreviewImage, value);
    }

    [ObservableProperty]
    private double _imageWidth;

    [ObservableProperty]
    private double _imageHeight;

    private void OnPreviewImageChanged(Bitmap? value)
    {
        if (value != null)
        {
            ImageWidth = value.Size.Width;
            ImageHeight = value.Size.Height;
            HasPreviewImage = true;
        }
        else
        {
            ImageWidth = 0;
            ImageHeight = 0;
            HasPreviewImage = false;
        }

        UpdateCanvasProperties();
    }

    [ObservableProperty]
    private double _previewPadding = 30;

    [ObservableProperty]
    private double _smartPadding;

    [ObservableProperty]
    private bool _useSmartPadding;

    public Thickness SmartPaddingThickness => new(SmartPadding);

    public IBrush SmartPaddingColor
    {
        get
        {
            if (PreviewImage == null || SmartPadding <= 0)
            {
                return Brushes.Transparent;
            }

            try
            {
                var skBitmap = PreviewImage.ToSKBitmap();
                if (skBitmap == null) return Brushes.Transparent;

                var color = skBitmap.GetPixel(0, 0);
                skBitmap.Dispose();
                return new SolidColorBrush(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
            }
            catch
            {
                return Brushes.Transparent;
            }
        }
    }

    [ObservableProperty]
    private double _previewCornerRadius = 15;

    [ObservableProperty]
    private double _shadowBlur = 30;

    [ObservableProperty]
    private double _zoom = 1.0;

    [ObservableProperty]
    private string _selectedColor = "#EF4444";

    partial void OnSelectedColorChanged(string value)
    {
        if (Color.TryParse(value, out var color))
        {
            switch (ActiveTool)
            {
                case EditorTool.Step:
                    Options.StepFillColor = color;
                    break;
                case EditorTool.Highlight:
                    Options.HighlighterColor = color;
                    break;
                default:
                    Options.BorderColor = color;
                    break;
            }
        }
    }

    [ObservableProperty]
    private int _strokeWidth = 4;

    partial void OnStrokeWidthChanged(int value)
    {
        Options.Thickness = value;
    }

    [ObservableProperty]
    private EditorTool _activeTool = EditorTool.Rectangle;

    partial void OnActiveToolChanged(EditorTool value)
    {
        switch (value)
        {
            case EditorTool.Rectangle:
            case EditorTool.Ellipse:
            case EditorTool.Line:
            case EditorTool.Arrow:
            case EditorTool.Freehand:
            case EditorTool.Text:
            case EditorTool.SpeechBalloon:
                SelectedColor = $"#{Options.BorderColor.A:X2}{Options.BorderColor.R:X2}{Options.BorderColor.G:X2}{Options.BorderColor.B:X2}";
                StrokeWidth = Options.Thickness;
                break;
            case EditorTool.Step:
                SelectedColor = $"#{Options.StepFillColor.A:X2}{Options.StepFillColor.R:X2}{Options.StepFillColor.G:X2}{Options.StepFillColor.B:X2}";
                StrokeWidth = Options.Thickness;
                break;
            case EditorTool.Highlight:
                SelectedColor = $"#{Options.HighlighterColor.A:X2}{Options.HighlighterColor.R:X2}{Options.HighlighterColor.G:X2}{Options.HighlighterColor.B:X2}";
                StrokeWidth = Options.Thickness;
                break;
        }
    }

    [ObservableProperty]
    private int _numberCounter = 1;

    [ObservableProperty]
    private string _selectedOutputRatio = OutputRatioAuto;

    [ObservableProperty]
    private double? _targetOutputAspectRatio;

    [ObservableProperty]
    private IBrush _canvasBackground;

    [ObservableProperty]
    private double _canvasCornerRadius;

    [ObservableProperty]
    private Thickness _canvasPadding;

    [ObservableProperty]
    private BoxShadows _canvasShadow;

    public EditorViewModel(EditorOptions? options = null)
    {
        _options = options ?? new EditorOptions();
        GradientPresets = BuildGradientPresets();
        _canvasBackground = CopyBrush(GradientPresets[1].Brush);
        UpdateCanvasProperties();
    }

    [RelayCommand]
    private void ResetNumberCounter() => NumberCounter = 1;

    [RelayCommand]
    private void SetOutputRatio(string ratioKey)
    {
        SelectedOutputRatio = string.IsNullOrWhiteSpace(ratioKey) ? OutputRatioAuto : ratioKey;
        TargetOutputAspectRatio = ParseAspectRatio(ratioKey);
        UpdateCanvasProperties();
    }

    [RelayCommand]
    private void ApplyGradientPreset(GradientPreset preset)
    {
        CanvasBackground = CopyBrush(preset.Brush);
    }

    [RelayCommand]
    private void SelectTool(EditorTool tool) => ActiveTool = tool;

    [RelayCommand]
    private void SetColor(string color) => SelectedColor = color;

    [RelayCommand]
    private void SetStrokeWidth(int width) => StrokeWidth = width;

    [RelayCommand]
    private void ZoomIn() => Zoom = Math.Clamp(Zoom + ZoomStep, MinZoom, MaxZoom);

    [RelayCommand]
    private void ZoomOut() => Zoom = Math.Clamp(Zoom - ZoomStep, MinZoom, MaxZoom);

    [RelayCommand]
    private void ResetZoom() => Zoom = 1.0;

    [RelayCommand]
    private void Undo() => UndoRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Redo() => RedoRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void DeleteSelected() => DeleteRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void ClearAnnotations() => ClearAnnotationsRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Copy() => CopyRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void QuickSave() => SaveRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void SaveAs() => SaveAsRequested?.Invoke(this, EventArgs.Empty);

    public void CropImage(int x, int y, int width, int height)
    {
        CropRequested?.Invoke(this, new CropEventArgs(x, y, width, height));
    }

    [RelayCommand]
    private void ApplyEffect() => ApplyEffectRequested?.Invoke(this, EventArgs.Empty);

    partial void OnPreviewPaddingChanged(double value) => UpdateCanvasProperties();

    partial void OnPreviewCornerRadiusChanged(double value) => UpdateCanvasProperties();

    partial void OnShadowBlurChanged(double value) => UpdateCanvasProperties();

    partial void OnSmartPaddingChanged(double value) => UpdateCanvasProperties();

    partial void OnUseSmartPaddingChanged(bool value) => UpdateCanvasProperties();

    private void UpdateCanvasProperties()
    {
        CanvasPadding = CalculateOutputPadding(PreviewPadding, TargetOutputAspectRatio);
        CanvasShadow = new BoxShadows(new BoxShadow
        {
            Blur = ShadowBlur,
            Color = Color.Parse("#20000000"),
            OffsetX = 0,
            OffsetY = 10
        });
        CanvasCornerRadius = Math.Max(0, PreviewCornerRadius);
        OnPropertyChanged(nameof(SmartPaddingColor));
        OnPropertyChanged(nameof(SmartPaddingThickness));
    }

    private static Thickness CalculateOutputPadding(double previewPadding, double? targetAspectRatio)
    {
        if (targetAspectRatio == null)
        {
            return new Thickness(previewPadding);
        }

        // Keep simple for now; just apply uniform padding when an aspect ratio is chosen.
        return new Thickness(previewPadding);
    }

    private static double? ParseAspectRatio(string ratio)
    {
        if (string.IsNullOrWhiteSpace(ratio) || ratio.Equals(OutputRatioAuto, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parts = ratio.Split(':');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], out var w) &&
            double.TryParse(parts[1], out var h) &&
            w > 0 && h > 0)
        {
            return w / h;
        }

        return null;
    }

    private static ObservableCollection<GradientPreset> BuildGradientPresets()
    {
        static LinearGradientBrush Make(string start, string end) => new()
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Color.Parse(start), 0),
                new GradientStop(Color.Parse(end), 1)
            }
        };

        return new ObservableCollection<GradientPreset>
        {
            new() { Name = "Sunset", Brush = Make("#F093FB", "#F5576C") },
            new() { Name = "Ocean", Brush = Make("#667EEA", "#764BA2") },
            new() { Name = "Forest", Brush = Make("#11998E", "#38EF7D") },
            new() { Name = "Fire", Brush = Make("#F12711", "#F5AF19") },
            new() { Name = "Cool Blue", Brush = Make("#2193B0", "#6DD5ED") },
            new() { Name = "Lavender", Brush = Make("#B8B8FF", "#D6A4FF") },
            new() { Name = "Aqua", Brush = Make("#13547A", "#80D0C7") },
            new() { Name = "Grape", Brush = Make("#7F00FF", "#E100FF") },
            new() { Name = "Peach", Brush = Make("#FFB88C", "#DE6262") },
            new() { Name = "Sky", Brush = Make("#56CCF2", "#2F80ED") },
            new() { Name = "Warm", Brush = Make("#F2994A", "#F2C94C") },
            new() { Name = "Mint", Brush = Make("#00B09B", "#96C93D") },
            new() { Name = "Midnight", Brush = Make("#232526", "#414345") },
            new() { Name = "Carbon", Brush = Make("#373B44", "#4286F4") },
            new() { Name = "Deep Space", Brush = Make("#000428", "#004E92") },
            new() { Name = "Noir", Brush = Make("#0F2027", "#2C5364") },
            new() { Name = "Royal", Brush = Make("#141E30", "#243B55") },
            new() { Name = "Rose Gold", Brush = Make("#E8CBC0", "#636FA4") },
            new() { Name = "Emerald", Brush = Make("#076585", "#FFFFFF") },
            new() { Name = "Amethyst", Brush = Make("#9D50BB", "#6E48AA") },
            new() { Name = "Neon", Brush = Make("#FF0844", "#FFB199") },
            new() { Name = "Aurora", Brush = Make("#00C9FF", "#92FE9D") },
            new() { Name = "Candy", Brush = Make("#D53369", "#DAAE51") },
            new() { Name = "Clean", Brush = new SolidColorBrush(Color.Parse("#FFFFFF")) }
        };
    }

    private static IBrush CopyBrush(IBrush brush)
    {
        switch (brush)
        {
            case SolidColorBrush solid:
                return new SolidColorBrush(solid.Color) { Opacity = solid.Opacity };
            case LinearGradientBrush linear:
                var stops = new GradientStops();
                foreach (var stop in linear.GradientStops)
                {
                    stops.Add(new GradientStop(stop.Color, stop.Offset));
                }

                return new LinearGradientBrush
                {
                    StartPoint = linear.StartPoint,
                    EndPoint = linear.EndPoint,
                    GradientStops = stops,
                    SpreadMethod = linear.SpreadMethod,
                    Opacity = linear.Opacity
                };
            default:
                return brush;
        }
    }
}
