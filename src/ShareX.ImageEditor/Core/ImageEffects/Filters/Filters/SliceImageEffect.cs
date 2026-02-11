using SkiaSharp;

namespace ShareX.ImageEditor.ImageEffects.Filters;

public class SliceImageEffect : ImageEffect
{
    public int MinHeight { get; set; }
    public int MaxHeight { get; set; }
    public int MinShift { get; set; }
    public int MaxShift { get; set; }

    public override string Name => "Slice";
    public override ImageEffectCategory Category => ImageEffectCategory.Filters;

    public SliceImageEffect(int minHeight, int maxHeight, int minShift, int maxShift)
    {
        MinHeight = minHeight;
        MaxHeight = maxHeight;
        MinShift = minShift;
        MaxShift = maxShift;
    }

    public override SKBitmap Apply(SKBitmap source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (MinHeight <= 0 || MaxHeight <= MinHeight) return source.Copy();

        Random rand = new Random();
        int maxAbsShift = Math.Max(Math.Abs(MinShift), Math.Abs(MaxShift));
        int newWidth = source.Width + maxAbsShift * 2;

        SKBitmap result = new SKBitmap(newWidth, source.Height);
        using SKCanvas canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);

        int y = 0;
        while (y < source.Height)
        {
            int sliceHeight = rand.Next(MinHeight, MaxHeight + 1);
            sliceHeight = Math.Min(sliceHeight, source.Height - y);

            int shift = rand.Next(MinShift, MaxShift + 1);

            SKRect srcRect = new SKRect(0, y, source.Width, y + sliceHeight);
            SKRect dstRect = new SKRect(maxAbsShift + shift, y, maxAbsShift + shift + source.Width, y + sliceHeight);

            canvas.DrawBitmap(source, srcRect, dstRect);

            y += sliceHeight;
        }

        return result;
    }
}

