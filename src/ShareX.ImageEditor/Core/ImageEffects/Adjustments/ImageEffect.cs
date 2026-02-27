using System.Diagnostics;
using SkiaSharp;

namespace ShareX.ImageEditor.ImageEffects.Adjustments;

public abstract class ImageEffect : ShareX.ImageEditor.ImageEffects.ImageEffect
{
    public override ImageEffectCategory Category => ImageEffectCategory.Adjustments;
    public override bool HasParameters => true;

    // --- GPU context (set by SKCanvasControl during Render) ---
    private static GRContext? _gpuContext;
    private const int GpuPixelThreshold = 160_000; // ≈ 400×400 px

    public static void SetGpuContext(GRContext? context)
    {
        bool wasNull = _gpuContext == null;
        bool isNull = context == null;
        _gpuContext = context;

        // Log only on state transitions to avoid noise on every Render() frame
        if (wasNull && !isNull)
            Debug.WriteLine("[ImageEffect] GRContext assigned by host — GPU acceleration ACTIVE");
        else if (!wasNull && isNull)
            Debug.WriteLine("[ImageEffect] GRContext cleared — falling back to software rendering");
    }

    protected static SKBitmap ApplyColorMatrix(SKBitmap source, float[] matrix)
    {
        using var filter = SKColorFilter.CreateColorMatrix(matrix);
        return ApplyColorFilter(source, filter);
    }

    protected static SKBitmap ApplyColorFilter(SKBitmap source, SKColorFilter filter)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        int pixels = source.Width * source.Height;

        // GPU path — only for images above the pixel threshold
        var grContext = pixels >= GpuPixelThreshold ? _gpuContext : null;
        if (grContext != null && !grContext.IsAbandoned)
        {
            Debug.WriteLine($"[ImageEffect] ApplyColorFilter GPU path — {source.Width}x{source.Height} ({pixels:N0} px)");
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
                    if (surface.ReadPixels(result.Info, result.GetPixels(), result.RowBytes, 0, 0))
                        return result;
                    result.Dispose();
                    Debug.WriteLine("[ImageEffect] ApplyColorFilter GPU ReadPixels failed — falling back to CPU");
                    // fall through to CPU path
                }
                else
                {
                    Debug.WriteLine("[ImageEffect] ApplyColorFilter GPU surface creation failed — falling back to CPU");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageEffect] ApplyColorFilter GPU exception — falling back to CPU: {ex.Message}");
            }
        }
        else if (_gpuContext == null)
        {
            Debug.WriteLine($"[ImageEffect] ApplyColorFilter CPU path — no GRContext (software renderer), {source.Width}x{source.Height}");
        }
        else if (pixels < GpuPixelThreshold)
        {
            Debug.WriteLine($"[ImageEffect] ApplyColorFilter CPU path — below threshold ({pixels:N0} px < {GpuPixelThreshold:N0}), {source.Width}x{source.Height}");
        }
        else
        {
            Debug.WriteLine($"[ImageEffect] ApplyColorFilter CPU path — GRContext abandoned, {source.Width}x{source.Height}");
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

