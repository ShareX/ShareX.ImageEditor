using SkiaSharp;

namespace ShareX.ImageEditor.ImageEffects.Filters;

public class PixelateImageEffect : ImageEffect
{
    public override string Name => "Pixelate";
    public override string IconKey => "IconGrid";
    public override bool HasParameters => true;
    public int Size { get; set; } = 10;

    public override SKBitmap Apply(SKBitmap source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (Size <= 1) return source.Copy();

        // Downscale then upscale to create pixelation effect
        int smallWidth = Math.Max(1, source.Width / Size);
        int smallHeight = Math.Max(1, source.Height / Size);

        SKBitmap small = new SKBitmap(smallWidth, smallHeight);
        using (SKCanvas smallCanvas = new SKCanvas(small))
        {
            smallCanvas.DrawBitmap(source, new SKRect(0, 0, smallWidth, smallHeight));
        }

        SKBitmap result = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
        using (SKCanvas canvas = new SKCanvas(result))
        {
#pragma warning disable CS0618 // SKFilterQuality is the correct 2.88.x API; SKSamplingOptions is 3.x only
            using SKPaint paint = new SKPaint { FilterQuality = SKFilterQuality.None };
#pragma warning restore CS0618
            canvas.DrawBitmap(small, new SKRect(0, 0, source.Width, source.Height), paint);
        }
        small.Dispose();
        return result;
    }
}

