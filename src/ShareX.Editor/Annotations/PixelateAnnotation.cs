using SkiaSharp;

namespace ShareX.Editor.Annotations;

/// <summary>
/// Pixelate annotation - applies pixelation to the region
/// </summary>
public class PixelateAnnotation : BaseEffectAnnotation
{
    public PixelateAnnotation()
    {
        ToolType = EditorTool.Pixelate;
        StrokeColor = "#00000000";
        StrokeWidth = 0;
        Amount = 10; // Default pixel size
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
                Color = new SKColor(128, 128, 128, 77),
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(rect, paint);
        }

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
        var skRect = new SKRectI((int)rect.Left, (int)rect.Top, (int)rect.Right, (int)rect.Bottom);
        skRect.Intersect(new SKRectI(0, 0, source.Width, source.Height));

        if (skRect.Width <= 0 || skRect.Height <= 0) return;

        using var crop = new SKBitmap(skRect.Width, skRect.Height);
        source.ExtractSubset(crop, skRect);

        // Pixelate logic: Downscale then upscale
        var pixelSize = (int)Math.Max(1, Amount);
        int w = Math.Max(1, crop.Width / pixelSize);
        int h = Math.Max(1, crop.Height / pixelSize);

        var info = new SKImageInfo(w, h);
        using var small = crop.Resize(info, SKFilterQuality.Low);

        info = new SKImageInfo(crop.Width, crop.Height);
        using var result = small.Resize(info, SKFilterQuality.None); // Nearest neighbor upscale

        EffectBitmap?.Dispose();
        EffectBitmap = result.Copy();
    }
}
