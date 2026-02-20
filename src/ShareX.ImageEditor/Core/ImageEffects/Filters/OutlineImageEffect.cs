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

        // Fast hardware-accelerated outline using Dilate filter
        using var dilate = SKImageFilter.CreateDilate(Size, Size);
        using var colorFilter = SKColorFilter.CreateBlendMode(Color, SKBlendMode.SrcIn);
        
        using SKPaint outlinePaint = new SKPaint
        {
            ImageFilter = SKImageFilter.CreateColorFilter(colorFilter, dilate)
        };

        // Draw dilated and colored outline
        canvas.DrawBitmap(source, totalExpand, totalExpand, outlinePaint);

        // Draw original image on top
        canvas.DrawBitmap(source, totalExpand, totalExpand);

        return result;
    }
}

