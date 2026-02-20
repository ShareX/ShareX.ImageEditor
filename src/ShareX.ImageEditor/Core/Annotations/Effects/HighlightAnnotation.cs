using SkiaSharp;

namespace ShareX.ImageEditor.Annotations;

/// <summary>
/// Highlight annotation - translucent color overlay
/// </summary>
public partial class HighlightAnnotation : BaseEffectAnnotation
{
    public HighlightAnnotation()
    {
        ToolType = EditorTool.Highlight;
        StrokeColor = "#FFFF00"; // Default yellow (opaque for logic, transparency comes from blend)
        StrokeWidth = 0; // No border by default
    }




    public override void UpdateEffect(SKBitmap source)
    {
        if (source == null) return;

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
            // Calculate draw offset
            int dx = intersect.Left - skRect.Left;
            int dy = intersect.Top - skRect.Top;

            var highlightColor = ParseColor(StrokeColor);

            using (var canvas = new SKCanvas(result))
            {
                // 1. Draw the original image portion
                var sourceRect = new SKRect(intersect.Left, intersect.Top, intersect.Right, intersect.Bottom);
                var destRect = new SKRect(dx, dy, dx + intersect.Width, dy + intersect.Height);
                canvas.DrawBitmap(source, sourceRect, destRect);

                // 2. Draw the highlight color with Darken blend mode over it
                // Darken mode effectively computes Math.Min for each color channel, matching old logic highly optimized!
                using var highlightPaint = new SKPaint
                {
                    Color = highlightColor,
                    Style = SKPaintStyle.Fill,
                    BlendMode = SKBlendMode.Darken
                };
                canvas.DrawRect(destRect, highlightPaint);
            }
        }

        EffectBitmap?.Dispose();
        EffectBitmap = result;
    }
}
