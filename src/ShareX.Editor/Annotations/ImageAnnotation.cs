using SkiaSharp;

namespace ShareX.Editor.Annotations;

/// <summary>
/// Image annotation - stickers or inserted images
/// </summary>
public class ImageAnnotation : Annotation
{
    private SKBitmap? _imageBitmap;

    /// <summary>
    /// File path to the image (if external)
    /// </summary>
    public string ImagePath { get; set; } = "";

    /// <summary>
    /// The loaded image bitmap
    /// </summary>
    public SKBitmap? ImageBitmap => _imageBitmap;

    public ImageAnnotation()
    {
        ToolType = EditorTool.Image;
        StrokeWidth = 0; // Usually no border
    }

    public void LoadImage(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                ImagePath = path;
                _imageBitmap?.Dispose();
                _imageBitmap = SKBitmap.Decode(path);
            }
            catch { }
        }
    }

    public void SetImage(SKBitmap bitmap)
    {
        _imageBitmap?.Dispose();
        _imageBitmap = bitmap;
    }

    public override void Render(SKCanvas canvas)
    {
        var rect = GetBounds();

        if (_imageBitmap != null)
        {
            canvas.DrawBitmap(_imageBitmap, rect);
        }
        else
        {
            // Placeholder
            using var dashPaint = new SKPaint
            {
                Color = SKColors.Gray,
                StrokeWidth = 2,
                Style = SKPaintStyle.Stroke,
                PathEffect = SKPathEffect.CreateDash(new float[] { 5, 5 }, 0)
            };
            canvas.DrawRect(rect, dashPaint);

            // Draw "Image" text placeholder
            using var textPaint = new SKPaint
            {
                Color = SKColors.Gray,
                TextSize = 12,
                TextAlign = SKTextAlign.Center,
                IsAntialias = true
            };
            canvas.DrawText("Image", rect.MidX, rect.MidY, textPaint);
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

    public override bool HitTest(SKPoint point, float tolerance = 5)
    {
        var bounds = GetBounds();
        var inflated = SKRect.Inflate(bounds, tolerance, tolerance);
        return inflated.Contains(point);
    }
}
