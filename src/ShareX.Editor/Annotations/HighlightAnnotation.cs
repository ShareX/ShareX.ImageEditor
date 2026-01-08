using Avalonia.Controls;
using Avalonia.Media;
using SkiaSharp;

namespace ShareX.Editor.Annotations;

/// <summary>
/// Highlight annotation - translucent color overlay
/// </summary>
public class HighlightAnnotation : BaseEffectAnnotation
{
    public HighlightAnnotation()
    {
        ToolType = EditorTool.Highlighter;
        StrokeColor = "#55FFFF00"; // Default yellow transparent
        StrokeWidth = 0; // No border by default
    }

    /// <summary>
    /// Creates the Avalonia visual for this annotation
    /// </summary>
    public Control CreateVisual()
    {
        var baseColor = Color.Parse(StrokeColor);
        // Apply consistent highlight alpha (0x55)
        var highlightColor = Color.FromArgb(0x55, baseColor.R, baseColor.G, baseColor.B);
        return new Avalonia.Controls.Shapes.Rectangle
        {
            Fill = new SolidColorBrush(highlightColor),
            Stroke = Brushes.Transparent,
            StrokeThickness = StrokeWidth,
            Tag = this
        };
    }

    public override void Render(SKCanvas canvas)
    {
        var rect = GetBounds();

        // Apply consistent highlight alpha regardless of incoming color alpha (matches Avalonia behavior)
        var baseColor = ParseColor(StrokeColor);
        var highlightColor = new SKColor(baseColor.Red, baseColor.Green, baseColor.Blue, 0x55);

        using var paint = new SKPaint
        {
            Color = highlightColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        canvas.DrawRect(rect, paint);
    }
}
