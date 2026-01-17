using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using SkiaSharp;

namespace ShareX.Editor.Annotations;

/// <summary>
/// Freehand pen/drawing annotation
/// ISSUE-013 fix: Implements IPointBasedAnnotation for unified polyline handling.
/// </summary>
public class FreehandAnnotation : Annotation, IPointBasedAnnotation
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
    /// <summary>
    /// Creates the Avalonia visual for this annotation
    /// </summary>
    public Control CreateVisual()
    {
        var brush = new SolidColorBrush(Color.Parse(StrokeColor));
        return new global::Avalonia.Controls.Shapes.Path
        {
            Stroke = brush,
            StrokeThickness = StrokeWidth,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Data = CreateSmoothedGeometry(),
            Tag = this
        };
    }

    public Geometry CreateSmoothedGeometry()
    {
        if (Points.Count < 2) return new StreamGeometry();

        var geometry = new StreamGeometry();
        using var context = geometry.Open();

        context.BeginFigure(new Point(Points[0].X, Points[0].Y), false);

        if (Points.Count == 2)
        {
            context.LineTo(new Point(Points[1].X, Points[1].Y));
        }
        else
        {
            var p0 = Points[0];
            var p1 = Points[1];
            var mid = new Point((p0.X + p1.X) / 2, (p0.Y + p1.Y) / 2);
            context.LineTo(mid);

            for (int i = 1; i < Points.Count - 1; i++)
            {
                var pControl = new Point(Points[i].X, Points[i].Y);
                var pNext = Points[i + 1];
                var nextMid = new Point((pControl.X + pNext.X) / 2, (pControl.Y + pNext.Y) / 2);

                context.QuadraticBezierTo(pControl, nextMid);
            }

            context.LineTo(new Point(Points[Points.Count - 1].X, Points[Points.Count - 1].Y));
        }
        
        context.EndFigure(false);
        return geometry;
    }

    public override void Render(SKCanvas canvas)
    {
        if (Points.Count < 2) return;

        using var paint = CreateStrokePaint();
        using var path = new SKPath();

        path.MoveTo(Points[0]);

        if (Points.Count == 2)
        {
            path.LineTo(Points[1]);
        }
        else
        {
            // Smooth curve algorithm (using quadratic bezier curves between midpoints)
            var p0 = Points[0];
            var p1 = Points[1];
            var mid = new SKPoint((p0.X + p1.X) / 2, (p0.Y + p1.Y) / 2);
            path.LineTo(mid);

            for (int i = 1; i < Points.Count - 1; i++)
            {
                var pControl = Points[i];
                var pNext = Points[i + 1];
                var nextMid = new SKPoint((pControl.X + pNext.X) / 2, (pControl.Y + pNext.Y) / 2);

                path.QuadTo(pControl, nextMid);
            }

            path.LineTo(Points[Points.Count - 1]);
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
