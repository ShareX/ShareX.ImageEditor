using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Skia;
using ShareX.ImageEditor.ImageEffects.Adjustments;
using ShareX.ImageEditor.Services;
using SkiaSharp;

namespace ShareX.ImageEditor.Controls;

/// <summary>
/// A control that allows direct SkiaSharp rendering into a WriteableBitmap.
/// This acts as the high-performance raster layer.
/// </summary>
public class SKCanvasControl : Control
{
    private WriteableBitmap? _bitmap;
    private object _lock = new object();

    // Cached lease feature and provider — registered once on first GPU-backed render.
    private ISkiaSharpApiLeaseFeature? _cachedLeaseFeature;
    private IEffectGpuLeaseProvider? _cachedLeaseProvider;

    /// <summary>
    /// Initializes or resizes the backing store.
    /// </summary>
    public void Initialize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;

        lock (_lock)
        {
            if (_bitmap?.PixelSize.Width == width && _bitmap?.PixelSize.Height == height)
                return;

            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        // Capture the GPU lease feature on the first render that exposes one.
        // ISkiaSharpApiLeaseFeature is backed by the long-lived GPU backend; each
        // Lease() call acquires the GL context lock and makes the context current
        // on the calling thread for the duration of the lease — safe from any thread.
        var leaseFeature = (context as IOptionalFeatureProvider)?.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature != null && leaseFeature != _cachedLeaseFeature)
        {
            _cachedLeaseFeature = leaseFeature;
            _cachedLeaseProvider = new SkiaSharpLeaseProvider(leaseFeature);
            ImageEffect.SetGpuLeaseProvider(_cachedLeaseProvider);
        }

        if (_bitmap != null)
            context.DrawImage(_bitmap, new Rect(0, 0, Bounds.Width, Bounds.Height));
    }

    /// <summary>
    /// Update the canvas using a SkiaSharp drawing action.
    /// </summary>
    public void Draw(Action<SKCanvas> drawAction)
    {
        if (_bitmap == null) return;

        lock (_lock)
        {
            using (var buffer = _bitmap.Lock())
            {
                var info = new SKImageInfo(
                    _bitmap.PixelSize.Width,
                    _bitmap.PixelSize.Height,
                    SKColorType.Bgra8888,
                    SKAlphaType.Premul);

                using (var surface = SKSurface.Create(info, buffer.Address, buffer.RowBytes))
                {
                    if (surface != null)
                        drawAction(surface.Canvas);
                }
            }
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual, Avalonia.Threading.DispatcherPriority.Render);
    }

    /// <summary>
    /// Releases resources.
    /// </summary>
    public void Dispose()
    {
        _bitmap?.Dispose();
        _bitmap = null;
    }

    /// <summary>
    /// Adapts <see cref="ISkiaSharpApiLeaseFeature"/> to <see cref="IEffectGpuLeaseProvider"/>.
    /// Each <see cref="TryWithGrContext"/> call acquires a fresh GL context lease via
    /// <see cref="ISkiaSharpApiLeaseFeature.Lease()"/>, which makes the context current on
    /// the calling thread and releases it on dispose.
    /// </summary>
    private sealed class SkiaSharpLeaseProvider : IEffectGpuLeaseProvider
    {
        private readonly ISkiaSharpApiLeaseFeature _leaseFeature;

        public SkiaSharpLeaseProvider(ISkiaSharpApiLeaseFeature leaseFeature)
            => _leaseFeature = leaseFeature;

        public SKBitmap? TryWithGrContext(Func<GRContext, SKBitmap?> gpuWork)
        {
            using var lease = _leaseFeature.Lease();
            var grContext = lease?.GrContext;
            if (grContext == null || grContext.IsAbandoned)
                return null;

            return gpuWork(grContext);
        }
    }
}
