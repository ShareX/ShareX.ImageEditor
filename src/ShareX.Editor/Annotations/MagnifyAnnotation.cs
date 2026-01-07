using SkiaSharp;

namespace ShareX.Editor.Annotations;

/// <summary>
/// Magnify annotation - zooms into the area
/// </summary>
public class MagnifyAnnotation : BaseEffectAnnotation
{
    public MagnifyAnnotation()
    {
        ToolType = EditorTool.Magnify;
        StrokeColor = "#FF000000"; // Black border
        StrokeWidth = 2;
        Amount = 2.0f; // Zoom level (2x)
    }

    public override void Render(SKCanvas canvas)
    {
        var rect = GetBounds();

        if (EffectBitmap != null)
        {
            canvas.DrawBitmap(EffectBitmap, rect.Left, rect.Top);
        }
        else
        {
            using var paint = new SKPaint
            {
                Color = new SKColor(211, 211, 211, 128), // LightGray with 50% opacity
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(rect, paint);
        }

        // Always draw border for magnifier
        using var strokePaint = CreateStrokePaint();
        canvas.DrawRect(rect, strokePaint);

        if (IsSelected)
        {
            using var selectPaint = new SKPaint
            {
                Color = SKColors.DodgerBlue,
                StrokeWidth = 2,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };
            canvas.DrawRect(rect, selectPaint);
        }
    }

    public override void UpdateEffect(SKBitmap source)
    {
        if (source == null) return;

        var rect = GetBounds();
        if (rect.Width <= 0 || rect.Height <= 0) return;

        // For magnification, capture a SMALLER area from the center and scale it UP
        float zoom = Math.Max(1.0f, Amount);
        float captureWidth = rect.Width / zoom;
        float captureHeight = rect.Height / zoom;

        float centerX = rect.MidX;
        float centerY = rect.MidY;

        float captureX = centerX - (captureWidth / 2);
        float captureY = centerY - (captureHeight / 2);

        var captureRect = new SKRectI((int)captureX, (int)captureY, (int)(captureX + captureWidth), (int)(captureY + captureHeight));

        // Ensure bounds validation
        var sourceBounds = new SKRectI(0, 0, source.Width, source.Height);
        captureRect.Intersect(sourceBounds);

        if (captureRect.Width <= 0 || captureRect.Height <= 0) return;

        using var crop = new SKBitmap(captureRect.Width, captureRect.Height);
        source.ExtractSubset(crop, captureRect);

        // Scale up to fill the full rect
        var info = new SKImageInfo((int)rect.Width, (int)rect.Height);
        using var scaled = crop.Resize(info, SKFilterQuality.Medium);

        EffectBitmap?.Dispose();
        EffectBitmap = scaled.Copy();
    }
}
