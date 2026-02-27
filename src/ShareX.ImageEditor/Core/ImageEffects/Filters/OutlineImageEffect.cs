using SkiaSharp;

namespace ShareX.ImageEditor.ImageEffects.Filters;

public class OutlineImageEffect : ImageEffect
{
    public int Size { get; set; }
    public int Padding { get; set; }
    public SKColor Color { get; set; }

    public override string Name => "Outline";
    public override ImageEffectCategory Category => ImageEffectCategory.Filters;

    public OutlineImageEffect(int size, int padding, SKColor color)
    {
        Size = size;
        Padding = padding;
        Color = color;
    }

    public override SKBitmap Apply(SKBitmap source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (Size <= 0) return source.Copy();

        int totalExpand = Size + Padding;
        int newWidth = source.Width + totalExpand * 2;
        int newHeight = source.Height + totalExpand * 2;

        SKBitmap result = new SKBitmap(newWidth, newHeight);
        using SKCanvas canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);

        // --- Optimised outline: single-pass morphological dilation ---
        // SKImageFilter.CreateDilate expands every opaque pixel by (Size) in all
        // directions entirely on the GPU / CPU via Skia's own convolution, replacing
        // the old O(Size²) loop that re-drew the full bitmap for every offset pixel.
        using SKImageFilter dilateFilter = SKImageFilter.CreateDilate(Size, Size);
        using SKImageFilter colorFilter = SKImageFilter.CreateColorFilter(
            SKColorFilter.CreateBlendMode(Color, SKBlendMode.SrcIn));
        using SKImageFilter outlineFilter = SKImageFilter.CreateCompose(colorFilter, dilateFilter);

        using SKPaint outlinePaint = new SKPaint
        {
            ImageFilter = outlineFilter
        };

        // Draw the dilated + tinted outline first…
        canvas.DrawBitmap(source, totalExpand, totalExpand, outlinePaint);

        // …then the original on top.
        canvas.DrawBitmap(source, totalExpand, totalExpand);

        return result;
    }
}
