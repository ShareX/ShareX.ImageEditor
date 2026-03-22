using SkiaSharp;

namespace ShareX.ImageEditor.Core.ImageEffects.Drawings;

public sealed class DrawLineEffect : ImageEffect
{
    public SKPointI StartPoint { get; set; } = new(0, 0);

    public SKPointI EndPoint { get; set; } = new(200, 0);

    public SKColor Color { get; set; } = new SKColor(255, 255, 255, 255);

    public float Thickness { get; set; } = 4f;

    public override string Name => "Line";

    public override ImageEffectCategory Category => ImageEffectCategory.Drawings;

    public override bool HasParameters => true;

    public override SKBitmap Apply(SKBitmap source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (Thickness <= 0 || Color.Alpha == 0)
        {
            return source.Copy();
        }

        SKBitmap result = source.Copy();
        using SKCanvas canvas = new(result);
        using SKPaint paint = new()
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            Color = Color,
            StrokeWidth = Thickness,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        canvas.DrawLine(StartPoint.X, StartPoint.Y, EndPoint.X, EndPoint.Y, paint);
        return result;
    }
}
