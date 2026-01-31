using SkiaSharp;

namespace ShareX.ImageEditor.ImageEffects.Filters;

public class ReflectionImageEffect : ImageEffect
{
    public int Percentage { get; set; }
    public int MaxAlpha { get; set; }
    public int MinAlpha { get; set; }
    public int Offset { get; set; }
    public bool Skew { get; set; }
    public int SkewSize { get; set; }

    public override string Name => "Reflection";
    public override ImageEffectCategory Category => ImageEffectCategory.Filters;

    public ReflectionImageEffect(int percentage, int maxAlpha, int minAlpha, int offset, bool skew, int skewSize)
    {
        Percentage = percentage;
        MaxAlpha = maxAlpha;
        MinAlpha = minAlpha;
        Offset = offset;
        Skew = skew;
        SkewSize = skewSize;
    }

    public override SKBitmap Apply(SKBitmap source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (Percentage <= 0) return source.Copy();

        int reflectionHeight = (int)(source.Height * Percentage / 100f);
        int newHeight = source.Height + Offset + reflectionHeight;

        SKBitmap result = new SKBitmap(source.Width, newHeight);
        using SKCanvas canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);

        // Draw original
        canvas.DrawBitmap(source, 0, 0);

        // Create reflection (flipped vertically)
        using SKBitmap flipped = new SKBitmap(source.Width, reflectionHeight);
        using (SKCanvas fc = new SKCanvas(flipped))
        {
            fc.Scale(1, -1, 0, reflectionHeight / 2f);
            fc.DrawBitmap(source, 0, 0);
        }

        // Apply gradient fade
        using SKPaint gradientPaint = new SKPaint();
        var gradient = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(0, reflectionHeight),
            new SKColor[] { new SKColor(255, 255, 255, (byte)MaxAlpha), new SKColor(255, 255, 255, (byte)MinAlpha) },
            null,
            SKShaderTileMode.Clamp);
        gradientPaint.Shader = gradient;
        gradientPaint.BlendMode = SKBlendMode.DstIn;

        using SKBitmap reflectionBitmap = new SKBitmap(source.Width, reflectionHeight);
        using (SKCanvas rc = new SKCanvas(reflectionBitmap))
        {
            rc.DrawBitmap(flipped, 0, 0);
            rc.DrawRect(new SKRect(0, 0, source.Width, reflectionHeight), gradientPaint);
        }

        // Apply skew if needed
        if (Skew && SkewSize > 0)
        {
            canvas.Save();
            canvas.Skew(SkewSize / 100f, 0);
        }

        canvas.DrawBitmap(reflectionBitmap, 0, source.Height + Offset);

        if (Skew && SkewSize > 0)
        {
            canvas.Restore();
        }

        return result;
    }
}

