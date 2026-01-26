using Avalonia.Controls;
using Avalonia.Media;
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

    /// <summary>
    /// Creates the Avalonia visual for this annotation
    /// </summary>
    public Control CreateVisual()
    {
        var brush = new SolidColorBrush(Color.Parse(StrokeColor));
        return new Avalonia.Controls.Shapes.Rectangle
        {
            Stroke = Brushes.Transparent,
            StrokeThickness = 0,
            Fill = Brushes.Transparent,
            Tag = this
        };
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

        // No border for magnifier as requested
        // using var strokePaint = CreateStrokePaint();
        // canvas.DrawRect(rect, strokePaint);

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
        int fullW = (int)rect.Width;
        int fullH = (int)rect.Height;
        if (fullW <= 0 || fullH <= 0) return;

        // Convert annotation bounds to integer rect
        var annotationRect = new SKRectI((int)rect.Left, (int)rect.Top, (int)rect.Right, (int)rect.Bottom);
        
        // Find intersection with source image bounds
        var validRect = annotationRect;
        validRect.Intersect(new SKRectI(0, 0, source.Width, source.Height));

        // Create result bitmap at FULL annotation size
        var result = new SKBitmap(fullW, fullH);
        result.Erase(SKColors.Transparent);

        if (validRect.Width <= 0 || validRect.Height <= 0)
        {
            EffectBitmap?.Dispose();
            EffectBitmap = result;
            return;
        }

        // For magnification, capture a SMALLER area from the CENTER OF THE VALID REGION and scale it UP
        // Use the valid region's center to avoid capturing outside the image
        float zoom = Math.Max(1.0f, Amount);
        
        // Calculate capture size based on valid region (not full annotation)
        float captureWidth = validRect.Width / zoom;
        float captureHeight = validRect.Height / zoom;

        // Center the capture within the valid region
        float centerX = validRect.Left + validRect.Width / 2f;
        float centerY = validRect.Top + validRect.Height / 2f;

        float captureX = centerX - (captureWidth / 2);
        float captureY = centerY - (captureHeight / 2);

        var captureRect = new SKRectI(
            (int)captureX, 
            (int)captureY, 
            (int)(captureX + captureWidth), 
            (int)(captureY + captureHeight)
        );

        // Ensure capture is within source bounds
        captureRect.Intersect(new SKRectI(0, 0, source.Width, source.Height));

        if (captureRect.Width <= 0 || captureRect.Height <= 0)
        {
            EffectBitmap?.Dispose();
            EffectBitmap = result;
            return;
        }

        using var crop = new SKBitmap(captureRect.Width, captureRect.Height);
        if (!source.ExtractSubset(crop, captureRect))
        {
            EffectBitmap?.Dispose();
            EffectBitmap = result;
            return;
        }

        // Scale capture to fill the VALID portion of the annotation
        var info = new SKImageInfo(validRect.Width, validRect.Height);
        using var scaled = crop.Resize(info, SKFilterQuality.Medium);

        if (scaled == null)
        {
            EffectBitmap?.Dispose();
            EffectBitmap = result;
            return;
        }

        // Draw scaled content at the correct offset within the full-size result
        int drawX = validRect.Left - annotationRect.Left;
        int drawY = validRect.Top - annotationRect.Top;

        using (var resultCanvas = new SKCanvas(result))
        {
            resultCanvas.DrawBitmap(scaled, drawX, drawY);
        }

        EffectBitmap?.Dispose();
        EffectBitmap = result;
    }
}
