using SkiaSharp;

namespace ShareX.ImageEditor.ImageEffects.Filters;

public class GlowImageEffect : ImageEffect
{
    public int Size { get; set; }
    public float Strength { get; set; }
    public SKColor Color { get; set; }
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }

    public override string Name => "Glow";

    public override ImageEffectCategory Category => ImageEffectCategory.Filters;

    public GlowImageEffect(int size, float strength, SKColor color, int offsetX, int offsetY)
    {
        Size = size;
        Strength = strength;
        Color = color;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    public override SKBitmap Apply(SKBitmap source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (Size <= 0) return source.Copy();

        int expand = Size + Math.Max(Math.Abs(OffsetX), Math.Abs(OffsetY));
        int newWidth = source.Width + expand * 2;
        int newHeight = source.Height + expand * 2;

        SKBitmap result = new SKBitmap(newWidth, newHeight);
        using SKCanvas canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);

        SKColor glowColor = Color.WithAlpha((byte)(255 * Strength / 100f));

        using SKPaint glowPaint = new SKPaint
        {
            ColorFilter = SKColorFilter.CreateBlendMode(glowColor, SKBlendMode.SrcIn),
            ImageFilter = SKImageFilter.CreateBlur(Size, Size)
        };

        // Draw glow
        canvas.DrawBitmap(source, expand + OffsetX, expand + OffsetY, glowPaint);
        // Draw original
        canvas.DrawBitmap(source, expand, expand);

        return result;
    }
}

