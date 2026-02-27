using SkiaSharp;

namespace ShareX.ImageEditor.ImageEffects.Adjustments;

public abstract class ImageEffect : ShareX.ImageEditor.ImageEffects.ImageEffect
{
    public override ImageEffectCategory Category => ImageEffectCategory.Adjustments;
    public override bool HasParameters => true;

    // --- GPU context (set by SKCanvasControl during Render) ---
    private static GRContext? _gpuContext;
    public static void SetGpuContext(GRContext? context) => _gpuContext = context;
    private const int GpuPixelThreshold = 160_000; // ≈ 400×400 px

    protected static SKBitmap ApplyColorMatrix(SKBitmap source, float[] matrix)
    {
        using var filter = SKColorFilter.CreateColorMatrix(matrix);
        return ApplyColorFilter(source, filter);
    }

    protected static SKBitmap ApplyColorFilter(SKBitmap source, SKColorFilter filter)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        // GPU path — only for images above the pixel threshold
        var grContext = source.Width * source.Height >= GpuPixelThreshold ? _gpuContext : null;
        if (grContext != null && !grContext.IsAbandoned)
        {
            try
            {
                var info = new SKImageInfo(source.Width, source.Height, source.ColorType, source.AlphaType);
                using var surface = SKSurface.Create(grContext, budgeted: true, info);
                if (surface != null)
                {
                    surface.Canvas.Clear(SKColors.Transparent);
                    using var gpuPaint = new SKPaint { ColorFilter = filter };
                    surface.Canvas.DrawBitmap(source, 0, 0, gpuPaint);
                    surface.Canvas.Flush();

                    var result = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
                    using var pixmap = result.PeekPixels();
                    if (pixmap != null && surface.ReadPixels(pixmap, 0, 0))
                        return result;
                    result.Dispose();
                    // fall through to CPU path
                }
            }
            catch { /* GPU failed; fall through to CPU path */ }
        }

        // CPU path (original)
        SKBitmap cpuResult = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
        using (SKCanvas canvas = new SKCanvas(cpuResult))
        {
            canvas.Clear(SKColors.Transparent);
            using (SKPaint paint = new SKPaint())
            {
                paint.ColorFilter = filter;
                canvas.DrawBitmap(source, 0, 0, paint);
            }
        }
        return cpuResult;
    }

    protected unsafe static SKBitmap ApplyPixelOperation(SKBitmap source, Func<SKColor, SKColor> operation)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        SKBitmap result = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);

        if (source.ColorType == SKColorType.Bgra8888)
        {
            int count = source.Width * source.Height;
            SKColor* srcPtr = (SKColor*)source.GetPixels();
            SKColor* dstPtr = (SKColor*)result.GetPixels();

            for (int i = 0; i < count; i++)
            {
                *dstPtr++ = operation(*srcPtr++);
            }
        }
        else
        {
            var srcPixels = source.Pixels;
            var dstPixels = new SKColor[srcPixels.Length];

            for (int i = 0; i < srcPixels.Length; i++)
            {
                dstPixels[i] = operation(srcPixels[i]);
            }
            
            result.Pixels = dstPixels;
        }

        return result;
    }
}

