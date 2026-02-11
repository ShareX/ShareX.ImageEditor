using SkiaSharp;

namespace ShareX.ImageEditor.ImageEffects.Filters;

public class ShadowImageEffect : ImageEffect
{
    public float Opacity { get; set; }
    public int Size { get; set; }
    public float Darkness { get; set; }
    public SKColor Color { get; set; }
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public bool AutoResize { get; set; }

    public override string Name => "Shadow";
    public override ImageEffectCategory Category => ImageEffectCategory.Filters;

    public ShadowImageEffect(float opacity, int size, float darkness, SKColor color, int offsetX, int offsetY, bool autoResize)
    {
        Opacity = opacity;
        Size = size;
        Darkness = darkness;
        Color = color;
        OffsetX = offsetX;
        OffsetY = offsetY;
        AutoResize = autoResize;
    }

    public override SKBitmap Apply(SKBitmap source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (Size <= 0 && !AutoResize) return source.Copy();

        int expandX = AutoResize ? Math.Abs(OffsetX) + Size : 0;
        int expandY = AutoResize ? Math.Abs(OffsetY) + Size : 0;
        int newWidth = source.Width + expandX * 2;
        int newHeight = source.Height + expandY * 2;

        SKBitmap result = new SKBitmap(newWidth, newHeight);
        using SKCanvas canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);

        int imageX = expandX + (AutoResize && OffsetX < 0 ? -OffsetX : 0);
        int imageY = expandY + (AutoResize && OffsetY < 0 ? -OffsetY : 0);
        int shadowX = imageX + OffsetX;
        int shadowY = imageY + OffsetY;

        // Create shadow
        SKColor shadowColor = new SKColor(
            (byte)(Color.Red * Darkness),
            (byte)(Color.Green * Darkness),
            (byte)(Color.Blue * Darkness),
            (byte)(255 * Opacity / 100f));

        using SKPaint shadowPaint = new SKPaint
        {
            ColorFilter = SKColorFilter.CreateBlendMode(shadowColor, SKBlendMode.SrcIn),
            ImageFilter = SKImageFilter.CreateBlur(Size / 2f, Size / 2f)
        };

        canvas.DrawBitmap(source, shadowX, shadowY, shadowPaint);
        canvas.DrawBitmap(source, imageX, imageY);

        return result;
    }
}

