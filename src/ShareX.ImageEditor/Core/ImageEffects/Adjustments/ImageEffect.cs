using SkiaSharp;
using ShareX.ImageEditor.Services;

namespace ShareX.ImageEditor.ImageEffects.Adjustments;

public abstract class ImageEffect : ShareX.ImageEditor.ImageEffects.ImageEffect
{
    public override ImageEffectCategory Category => ImageEffectCategory.Adjustments;
    public override bool HasParameters => true;

    // Set by the host (EditorView.OnLoaded) via SetGpuLeaseProvider().
    // The provider wraps ISkiaGpuWithPlatformGraphicsContext.TryGetGrContext(), acquiring the
    // GL context lock and making it current on the calling thread for the duration of each call.
    // Null if no GPU backend is available or the editor has not yet registered one.
    private static IEffectGpuLeaseProvider? _gpuLeaseProvider;

    // Images below this pixel count always use CPU — GPU upload + readback overhead
    // exceeds the rendering cost for small bitmaps.
    private const int GpuPixelThreshold = 160_000; // ≈ 400×400 px

    /// <summary>
    /// Registers the GPU lease provider. Called by the host (EditorView) when loaded.
    /// Pass <c>null</c> to deregister on unload.
    /// </summary>
    public static void SetGpuLeaseProvider(IEffectGpuLeaseProvider? provider)
    {
        bool wasNull = _gpuLeaseProvider == null;
        bool isNull = provider == null;
        _gpuLeaseProvider = provider;

        if (wasNull && !isNull)
            EditorServices.ReportInformation(nameof(ImageEffect), "GPU lease provider registered; GPU path active.");
        else if (!wasNull && isNull)
            EditorServices.ReportInformation(nameof(ImageEffect), "GPU lease provider removed; effects using CPU path.");
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

        // GPU path — attempted when a provider is registered and the image is large enough.
        if (_gpuLeaseProvider != null && pixels >= GpuPixelThreshold)
        {
            EditorServices.ReportInformation(nameof(ImageEffect),
                $"ApplyColorFilter GPU path ({source.Width}x{source.Height}, {pixels:N0} px).");

            try
            {
                // TryWithGrContext acquires the GL context lock, runs the delegate, then releases.
                var gpuResult = _gpuLeaseProvider.TryWithGrContext(grContext =>
                {
                    if (grContext.IsAbandoned)
                        return null;

                    var info = new SKImageInfo(source.Width, source.Height, source.ColorType, source.AlphaType);
                    using var surface = SKSurface.Create(grContext, budgeted: true, info);
                    if (surface == null)
                        return null;

                    surface.Canvas.Clear(SKColors.Transparent);
                    using var gpuPaint = new SKPaint { ColorFilter = filter };
                    surface.Canvas.DrawBitmap(source, 0, 0, gpuPaint);
                    surface.Canvas.Flush();

                    using var gpuImage = surface.Snapshot();
                    return gpuImage != null ? SKBitmap.FromImage(gpuImage) : null;
                });

                if (gpuResult != null)
                    return gpuResult;

                EditorServices.ReportWarning(nameof(ImageEffect),
                    "ApplyColorFilter GPU path returned null; falling back to CPU.");
            }
            catch (Exception ex)
            {
                EditorServices.ReportWarning(nameof(ImageEffect),
                    "ApplyColorFilter GPU exception; falling back to CPU.", ex);
            }
        }

        // CPU path
        var cpuResult = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
        using (var canvas = new SKCanvas(cpuResult))
        {
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { ColorFilter = filter };
            canvas.DrawBitmap(source, 0, 0, paint);
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
