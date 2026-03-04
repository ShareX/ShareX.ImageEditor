using SkiaSharp;
using ShareX.ImageEditor.Services;
using System.Threading;

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
    private static int _cpuNoProviderDiagnosticSent;
    private static int _cpuSmallImageDiagnosticSent;
    private static int _gpuSuccessDiagnosticSent;

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
        {
            Interlocked.Exchange(ref _gpuSuccessDiagnosticSent, 0);
            EditorServices.ReportInformation(nameof(ImageEffect), "GPU lease provider registered; GPU path active.");
        }
        else if (!wasNull && isNull)
        {
            Interlocked.Exchange(ref _cpuNoProviderDiagnosticSent, 0);
            EditorServices.ReportInformation(nameof(ImageEffect), "GPU lease provider removed; effects using CPU path.");
        }
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

        // First preference: host-provided Option B GPU context (persistent off-screen GRContext).
        var hostGpuProvider = EditorServices.GpuContextProvider;
        if (hostGpuProvider != null && pixels >= GpuPixelThreshold)
        {
            try
            {
                var gpuResult = hostGpuProvider.TryRunColorFilter(source, filter, pixels, nameof(ApplyColorFilter));
                if (gpuResult != null)
                {
                    ReportInformationOnce(ref _gpuSuccessDiagnosticSent,
                        "ApplyColorFilter GPU path succeeded via host GPU context provider (Option B).");
                    return gpuResult;
                }

                EditorServices.ReportWarning(nameof(ImageEffect),
                    "ApplyColorFilter host GPU context provider returned null; falling back to legacy GPU/CPU path.");
            }
            catch (Exception ex)
            {
                EditorServices.ReportWarning(nameof(ImageEffect),
                    "ApplyColorFilter host GPU context provider threw; falling back to legacy GPU/CPU path.", ex);
            }
        }

        // Legacy GPU path — render-thread lease provider (Option A), kept as a secondary path.
        IEffectGpuLeaseProvider? leaseProvider = _gpuLeaseProvider;
        if (leaseProvider != null && pixels >= GpuPixelThreshold)
        {
            EditorServices.ReportInformation(nameof(ImageEffect),
                $"ApplyColorFilter attempting GPU path via render-thread lease provider ({source.Width}x{source.Height}, {pixels:N0} px).");

            try
            {
                // TryWithGrContext acquires the GL context lock, runs the delegate, then releases.
                var gpuResult = leaseProvider.TryWithGrContext(grContext =>
                {
                    if (grContext.IsAbandoned)
                        return null;

                    // Rgba8888 is universally supported across GL and GLES backends.
                    // Bgra8888 is not a valid GPU render target on GLES (e.g. Intel UHD/Mesa).
                    var gpuInfo = new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, source.AlphaType);
                    using var surface = SKSurface.Create(grContext, budgeted: true, gpuInfo);
                    if (surface == null)
                        return null;

                    surface.Canvas.Clear(SKColors.Transparent);
                    using var gpuPaint = new SKPaint { ColorFilter = filter };
                    surface.Canvas.DrawBitmap(source, 0, 0, gpuPaint);
                    surface.Canvas.Flush();

                    using var gpuImage = surface.Snapshot();
                    if (gpuImage == null) return null;

                    // Read back into the source's original color type so the rest of the
                    // pipeline receives the expected format (e.g. Bgra8888 for the canvas).
                    var result = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);
                    if (gpuImage.ReadPixels(result.Info, result.GetPixels(), result.RowBytes, 0, 0))
                        return result;
                    result.Dispose();
                    return null;
                });

                if (gpuResult != null)
                {
                    ReportInformationOnce(ref _gpuSuccessDiagnosticSent,
                        "ApplyColorFilter GPU path succeeded via render-thread lease provider.");
                    return gpuResult;
                }

                EditorServices.ReportWarning(nameof(ImageEffect),
                    "ApplyColorFilter render-thread lease provider returned null; falling back to CPU.");
            }
            catch (Exception ex)
            {
                EditorServices.ReportWarning(nameof(ImageEffect),
                    "ApplyColorFilter render-thread lease provider threw; falling back to CPU.", ex);
            }
        }

        if (leaseProvider == null && hostGpuProvider == null)
        {
            ReportInformationOnce(ref _cpuNoProviderDiagnosticSent,
                "ApplyColorFilter using CPU path because no GPU provider is registered.");
        }
        else if (pixels < GpuPixelThreshold)
        {
            ReportInformationOnce(ref _cpuSmallImageDiagnosticSent,
                $"ApplyColorFilter using CPU path for small images below {GpuPixelThreshold:N0} px.");
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

    private static void ReportInformationOnce(ref int gate, string message)
    {
        if (Interlocked.CompareExchange(ref gate, 1, 0) == 0)
        {
            EditorServices.ReportInformation(nameof(ImageEffect), message);
        }
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
