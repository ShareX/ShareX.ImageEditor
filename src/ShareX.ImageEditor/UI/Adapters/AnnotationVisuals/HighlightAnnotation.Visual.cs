using Avalonia.Controls;
using Avalonia.Media;

namespace ShareX.ImageEditor.Annotations;

public partial class HighlightAnnotation
{
    /// <summary>
    /// Creates the Avalonia visual for this annotation.
    /// </summary>
    public Control CreateVisual()
    {
        var baseColor = Color.Parse(StrokeColor);
        var highlightColor = Color.FromArgb(0x55, baseColor.R, baseColor.G, baseColor.B);

        return new Avalonia.Controls.Shapes.Rectangle
        {
            Fill = new SolidColorBrush(highlightColor),
            Stroke = Brushes.Transparent,
            StrokeThickness = 0,
            Tag = this
        };
    }
}
