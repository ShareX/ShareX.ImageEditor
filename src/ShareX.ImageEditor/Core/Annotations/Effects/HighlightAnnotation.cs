using SkiaSharp;

namespace ShareX.ImageEditor.Core.Annotations;

/// <summary>
/// Highlight annotation - translucent color overlay
/// </summary>
public partial class HighlightAnnotation : BaseEffectAnnotation
{
    public HighlightAnnotation()
    {
        ToolType = EditorTool.Highlight;
        StrokeColor = "Transparent";
        FillColor = "#FFFF00"; // Default yellow (opaque for logic, transparency comes from blend)
        StrokeWidth = 0; // No border by default
    }

    internal override string GetInteractionCacheKey()
    {
        return $"{base.GetInteractionCacheKey()}:{FillColor}";
    }

    internal static SKBitmap? CreateHighlightedSourceCache(SKBitmap source, string? fillColor)
    {
        if (source == null) return null;

        var highlightedSource = source.Copy();
        ApplyHighlightToBitmap(highlightedSource, fillColor);
        return highlightedSource;
    }

    internal override SKBitmap? CreateInteractionCacheBitmap(SKBitmap source)
    {
        return CreateHighlightedSourceCache(source, FillColor);
    }

    internal override void UpdateEffectFromInteractionCache(SKBitmap source, SKBitmap cachedEffectBitmap)
    {
        if (RotationAngle != 0)
        {
            UpdateEffectFromProcessedSource(source, cachedEffectBitmap);
            return;
        }

        UpdateEffectFromAlignedCache(source, cachedEffectBitmap);
    }

    public override void UpdateEffect(SKBitmap source)
    {
        if (source == null) return;

        if (RotationAngle != 0)
        {
            using var highlightedSource = CreateHighlightedSourceCache(source, FillColor);
            if (highlightedSource == null) return;

            UpdateEffectFromProcessedSource(source, highlightedSource);
            return;
        }

        var rect = GetBounds();
        var fullW = (int)rect.Width;
        var fullH = (int)rect.Height;

        if (fullW <= 0 || fullH <= 0) return;

        // Logical intersection with image
        var skRect = new SKRectI((int)rect.Left, (int)rect.Top, (int)rect.Right, (int)rect.Bottom);
        var intersect = skRect;
        intersect.Intersect(new SKRectI(0, 0, source.Width, source.Height));

        // Create the FULL size bitmap (matching rect)
        var result = new SKBitmap(fullW, fullH);
        result.Erase(SKColors.Transparent);

        // If specific intersection exists, process it
        if (intersect.Width > 0 && intersect.Height > 0)
        {
            // Extract the valid region to highlight
            using var crop = new SKBitmap(intersect.Width, intersect.Height);
            source.ExtractSubset(crop, intersect);

            // We will modify a copy of the crop (so we don't affect source)
            using var processedCrop = crop.Copy();
            ApplyHighlightToBitmap(processedCrop, FillColor);

            // Draw the processed crop into the result at the correct offset
            // Offset is difference between intersection left/top and annotation left/top
            int dx = intersect.Left - skRect.Left;
            int dy = intersect.Top - skRect.Top;

            using (var canvas = new SKCanvas(result))
            {
                canvas.DrawBitmap(processedCrop, dx, dy);
            }
        }

        EffectBitmap?.Dispose();
        EffectBitmap = result;
    }

    private static unsafe void ApplyHighlightToBitmap(SKBitmap bitmap, string? fillColor)
    {
        if (bitmap == null) return;

        var highlightColor = SKColor.Parse(fillColor ?? "#FFFF00");
        byte r = highlightColor.Red;
        byte g = highlightColor.Green;
        byte b = highlightColor.Blue;

        var pixels = (byte*)bitmap.GetPixels();
        int count = bitmap.Width * bitmap.Height;
        var colorType = bitmap.ColorType;

        if (colorType == SKColorType.Bgra8888 || colorType == SKColorType.Rgba8888)
        {
            if (colorType == SKColorType.Bgra8888)
            {
                for (int i = 0; i < count; i++)
                {
                    int offset = i * 4;
                    pixels[offset] = Math.Min(pixels[offset], b);
                    pixels[offset + 1] = Math.Min(pixels[offset + 1], g);
                    pixels[offset + 2] = Math.Min(pixels[offset + 2], r);
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    int offset = i * 4;
                    pixels[offset] = Math.Min(pixels[offset], r);
                    pixels[offset + 1] = Math.Min(pixels[offset + 1], g);
                    pixels[offset + 2] = Math.Min(pixels[offset + 2], b);
                }
            }

            return;
        }

        for (int x = 0; x < bitmap.Width; x++)
        {
            for (int y = 0; y < bitmap.Height; y++)
            {
                var color = bitmap.GetPixel(x, y);
                var newColor = new SKColor(
                    Math.Min(color.Red, r),
                    Math.Min(color.Green, g),
                    Math.Min(color.Blue, b),
                    color.Alpha);
                bitmap.SetPixel(x, y, newColor);
            }
        }
    }
}
