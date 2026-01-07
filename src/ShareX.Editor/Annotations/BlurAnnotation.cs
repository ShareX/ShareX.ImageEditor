using SkiaSharp;

namespace ShareX.Editor.Annotations;

/// <summary>
/// Blur annotation - applies blur to the region
/// </summary>
public class BlurAnnotation : BaseEffectAnnotation
{
    public BlurAnnotation()
    {
        ToolType = EditorTool.Blur;
        StrokeColor = "#00000000"; // Transparent border
        StrokeWidth = 0;
        Amount = 10; // Default blur radius
    }

    public override void Render(SKCanvas canvas)
    {
        var rect = GetBounds();

        if (EffectBitmap != null)
        {
            // Draw the pre-calculated blurred image
            canvas.DrawBitmap(EffectBitmap, rect.Left, rect.Top);
        }
        else
        {
            // Fallback: draw translucent placeholder if effect not generated yet
            using var paint = new SKPaint
            {
                Color = new SKColor(128, 128, 128, 77), // Gray with 30% opacity
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(rect, paint);
        }

        // Draw selection border if selected
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

    /// <summary>
    /// Update the internal blurred bitmap based on the source image
    /// </summary>
    /// <param name="source">The full source image (SKBitmap)</param>
    public override void UpdateEffect(SKBitmap source)
    {
        if (source == null) return;

        var rect = GetBounds();
        if (rect.Width <= 0 || rect.Height <= 0) return;

        // Convert to integer bounds
        var skRect = new SKRectI((int)rect.Left, (int)rect.Top, (int)rect.Right, (int)rect.Bottom);

        // Ensure bounds are valid
        skRect.Intersect(new SKRectI(0, 0, source.Width, source.Height));

        if (skRect.Width <= 0 || skRect.Height <= 0) return;

        // Crop the region
        using var crop = new SKBitmap(skRect.Width, skRect.Height);
        source.ExtractSubset(crop, skRect);

        // Apply Blur
        var blurRadius = (int)Amount;

        using var surface = SKSurface.Create(new SKImageInfo(crop.Width, crop.Height));
        var canvas = surface.Canvas;
        using var paint = new SKPaint();
        paint.ImageFilter = SKImageFilter.CreateBlur(blurRadius, blurRadius);

        canvas.DrawBitmap(crop, 0, 0, paint);

        using var blurredImage = surface.Snapshot();

        // Store as SKBitmap
        EffectBitmap?.Dispose();
        EffectBitmap = SKBitmap.FromImage(blurredImage);
    }
}
