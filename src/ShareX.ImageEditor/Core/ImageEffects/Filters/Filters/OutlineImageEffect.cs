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

        // Create outline by drawing image multiple times offset in all directions
        using SKPaint outlinePaint = new SKPaint { ColorFilter = SKColorFilter.CreateBlendMode(Color, SKBlendMode.SrcIn) };

        // Draw outline copies
        for (int dx = -Size; dx <= Size; dx++)
        {
            for (int dy = -Size; dy <= Size; dy++)
            {
                if (dx * dx + dy * dy <= Size * Size) // Circular outline
                {
                    canvas.DrawBitmap(source, totalExpand + dx, totalExpand + dy, outlinePaint);
                }
            }
        }

        // Draw original image on top
        canvas.DrawBitmap(source, totalExpand, totalExpand);

        return result;
    }
}

