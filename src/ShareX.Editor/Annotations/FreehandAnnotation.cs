using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using SkiaSharp;

namespace ShareX.Editor.Annotations;

/// <summary>
/// Freehand pen/drawing annotation
/// </summary>
public class FreehandAnnotation : Annotation
{
    public List<SKPoint> Points { get; set; } = new List<SKPoint>();

    /// <summary>
    /// Simplification tolerance for smoothing
    /// </summary>
    public float SmoothingTolerance { get; set; } = 2.0f;

    public FreehandAnnotation()
    {
        ToolType = EditorTool.Pen;
    }

    public override Annotation Clone()
    {
        var clone = (FreehandAnnotation)base.Clone();
        clone.Points = new List<SKPoint>(Points); // Deep copy the points list
        return clone;
    }

    /// <summary>
    /// Creates the Avalonia visual for this annotation
    /// </summary>
    public Control CreateVisual()
    {
        var brush = new SolidColorBrush(Color.Parse(StrokeColor));
        return new Polyline
        {
            Stroke = brush,
            StrokeThickness = StrokeWidth,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Tag = this
        };
    }

    public override void Render(SKCanvas canvas)
    {
        if (Points.Count < 2) return;

        using var paint = CreateStrokePaint();
        using var path = new SKPath();

        path.MoveTo(Points[0]);
        for (int i = 1; i < Points.Count; i++)
        {
            path.LineTo(Points[i]);
        }

        canvas.DrawPath(path, paint);
    }

    public override bool HitTest(SKPoint point, float tolerance = 5)
    {
        // Simple bounding box check first
        var bounds = GetBounds();
        var inflatedBounds = SKRect.Inflate(bounds, tolerance, tolerance);
        if (!inflatedBounds.Contains(point)) return false;

        // Detailed segment check
        for (int i = 0; i < Points.Count - 1; i++)
        {
            if (DistanceToSegment(point, Points[i], Points[i + 1]) <= tolerance)
                return true;
        }

        return false;
    }

    public override SKRect GetBounds()
    {
        if (Points.Count == 0) return SKRect.Empty;

        float minX = Points.Min(p => p.X);
        float minY = Points.Min(p => p.Y);
        float maxX = Points.Max(p => p.X);
        float maxY = Points.Max(p => p.Y);

        return new SKRect(minX, minY, maxX, maxY);
    }

    private float DistanceToSegment(SKPoint p, SKPoint v, SKPoint w)
    {
        float l2 = (v.X - w.X) * (v.X - w.X) + (v.Y - w.Y) * (v.Y - w.Y);
        if (l2 == 0) return Distance(p, v);

        float t = ((p.X - v.X) * (w.X - v.X) + (p.Y - v.Y) * (w.Y - v.Y)) / l2;
        t = Math.Max(0, Math.Min(1, t));

        var projection = new SKPoint(v.X + t * (w.X - v.X), v.Y + t * (w.Y - v.Y));
        return Distance(p, projection);
    }

    private float Distance(SKPoint p1, SKPoint p2)
    {
        return (float)Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
    }
}
