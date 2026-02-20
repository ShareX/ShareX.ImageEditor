using SkiaSharp;

namespace ShareX.ImageEditor.ImageEffects.Adjustments;

public abstract class ImageEffect : ShareX.ImageEditor.ImageEffects.ImageEffect
{
    public override ImageEffectCategory Category => ImageEffectCategory.Adjustments;
    public override bool HasParameters => true;

    protected static SKBitmap ApplyColorMatrix(SKBitmap source, float[] matrix)
    {
        using var filter = SKColorFilter.CreateColorMatrix(matrix);
        return ApplyColorFilter(source, filter);
    }

    protected static SKBitmap ApplyColorFilter(SKBitmap source, SKColorFilter filter)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        SKBitmap result = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
        using (SKCanvas canvas = new SKCanvas(result))
        {
            canvas.Clear(SKColors.Transparent);
            using (SKPaint paint = new SKPaint())
            {
                paint.ColorFilter = filter;
                canvas.DrawBitmap(source, 0, 0, paint);
            }
        }
        return result;
    }

    protected static SKBitmap ApplyPixelOperation(SKBitmap source, Func<SKColor, SKColor> operation)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        SKBitmap result = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);

        if (source.ColorType == SKColorType.Bgra8888 || source.ColorType == SKColorType.Rgba8888)
        {
            unsafe
            {
                byte* srcPtr = (byte*)source.GetPixels();
                byte* dstPtr = (byte*)result.GetPixels();
                int length = source.Width * source.Height * source.BytesPerPixel;
                
                int rIdx = source.ColorType == SKColorType.Bgra8888 ? 2 : 0;
                int bIdx = source.ColorType == SKColorType.Bgra8888 ? 0 : 2;

                for (int i = 0; i < length; i += 4)
                {
                    byte b = srcPtr[i + bIdx];
                    byte g = srcPtr[i + 1];
                    byte r = srcPtr[i + rIdx];
                    byte a = srcPtr[i + 3];
                    
                    var modified = operation(new SKColor(r, g, b, a));

                    dstPtr[i + bIdx] = modified.Blue;
                    dstPtr[i + 1] = modified.Green;
                    dstPtr[i + rIdx] = modified.Red;
                    dstPtr[i + 3] = modified.Alpha;
                }
            }
        }
        else
        {
            for (int x = 0; x < source.Width; x++)
            {
                for (int y = 0; y < source.Height; y++)
                {
                    SKColor original = source.GetPixel(x, y);
                    SKColor modified = operation(original);
                    result.SetPixel(x, y, modified);
                }
            }
        }

        return result;
    }
}

