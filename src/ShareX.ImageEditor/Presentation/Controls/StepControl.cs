using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ShareX.ImageEditor.Core.Annotations;
using SkiaSharp;

namespace ShareX.ImageEditor.Presentation.Controls;

public class StepControl : Control
{
    public static readonly StyledProperty<NumberAnnotation?> AnnotationProperty =
        AvaloniaProperty.Register<StepControl, NumberAnnotation?>(nameof(Annotation));

    public NumberAnnotation? Annotation
    {
        get => GetValue(AnnotationProperty);
        set => SetValue(AnnotationProperty, value);
    }

    static StepControl()
    {
        AffectsRender<StepControl>(AnnotationProperty);
        AffectsMeasure<StepControl>(AnnotationProperty);
    }

    public StepControl()
    {
        ClipToBounds = false;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Annotation == null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var visualBounds = Annotation.GetInteractionBounds();
        var bodyBounds = Annotation.GetBounds();
        var bodyGeometry = new EllipseGeometry(new Rect(
            bodyBounds.Left - visualBounds.Left,
            bodyBounds.Top - visualBounds.Top,
            Math.Max(bodyBounds.Width, 2),
            Math.Max(bodyBounds.Height, 2)));
        var tailGeometry = CreateTailGeometry(Annotation);
        Geometry geometry = tailGeometry != null
            ? new CombinedGeometry(GeometryCombineMode.Union, bodyGeometry, tailGeometry)
            : bodyGeometry;

        var strokeBrush = new SolidColorBrush(Color.Parse(Annotation.StrokeColor));
        var fillBrush = new SolidColorBrush(Color.Parse(Annotation.FillColor));
        var strokePen = new Pen(strokeBrush, Annotation.StrokeWidth);

        context.DrawGeometry(fillBrush, null, geometry);
        context.DrawGeometry(null, strokePen, geometry);

        var formattedText = new FormattedText(
            Annotation.DisplayText,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, Annotation.IsBold ? FontWeight.Bold : FontWeight.Normal),
            Annotation.FontSize * 0.6,
            new SolidColorBrush(Color.Parse(Annotation.TextColor)));

        var textX = bodyBounds.Left - visualBounds.Left + ((bodyBounds.Width - formattedText.Width) / 2);
        var textY = bodyBounds.Top - visualBounds.Top + ((bodyBounds.Height - formattedText.Height) / 2);
        context.DrawText(formattedText, new Point(textX, textY));
    }

    private static Geometry? CreateTailGeometry(NumberAnnotation annotation)
    {
        return annotation.TailStyle switch
        {
            StepTailStyle.Arrow => CreateArrowTailGeometry(annotation),
            _ => CreateTriangleTailGeometry(annotation)
        };
    }

    private static Geometry? CreateTriangleTailGeometry(NumberAnnotation annotation)
    {
        if (!annotation.TryGetTailPolygon(out var tailBaseStart, out var tailTip, out var tailBaseEnd))
        {
            return null;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(ToRenderPoint(annotation, tailBaseStart), true);
            ctx.LineTo(ToRenderPoint(annotation, tailTip));
            ctx.LineTo(ToRenderPoint(annotation, tailBaseEnd));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry? CreateArrowTailGeometry(NumberAnnotation annotation)
    {
        if (!annotation.TryGetArrowTailOutline(
            out var shaftStartLeft,
            out var shaftEndLeft,
            out var wingLeft,
            out var tailTip,
            out var wingRight,
            out var shaftEndRight,
            out var shaftStartRight))
        {
            return null;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(ToRenderPoint(annotation, shaftStartLeft), true);
            ctx.LineTo(ToRenderPoint(annotation, shaftEndLeft));
            ctx.LineTo(ToRenderPoint(annotation, wingLeft));
            ctx.LineTo(ToRenderPoint(annotation, tailTip));
            ctx.LineTo(ToRenderPoint(annotation, wingRight));
            ctx.LineTo(ToRenderPoint(annotation, shaftEndRight));
            ctx.LineTo(ToRenderPoint(annotation, shaftStartRight));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Point ToRenderPoint(NumberAnnotation annotation, SKPoint point)
    {
        var bounds = annotation.GetInteractionBounds();
        return new Point(point.X - bounds.Left, point.Y - bounds.Top);
    }
}
