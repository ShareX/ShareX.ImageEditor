using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
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
    private bool _lastRenderHadGpu = false;

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
            // Create a WriteableBitmap with Bgra8888 which is standard for Skia/Avalonia interop
            _bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        // Capture GPU context via a custom draw operation.
        // In Avalonia 11, DrawingContext.PlatformImpl is not public; use ICustomDrawOperation
        // + ISkiaSharpApiLeaseFeature to access the active GRContext from within the render pipeline.
        context.Custom(new GpuContextCapture(this));

        // Draw the bitmap to the control's bounds
        // We use the full bounds to ensure the image stretches if needed, though usually this control size matches image size
        if (_bitmap != null)
        {
            context.DrawImage(_bitmap, new Rect(0, 0, Bounds.Width, Bounds.Height));
        }
    }

    /// <summary>
    /// Zero-size custom draw op that runs inside Avalonia's render pipeline to capture
    /// the active <see cref="GRContext"/> and forward it to the ImageEffect pipeline.
    /// </summary>
    private sealed class GpuContextCapture : ICustomDrawOperation
    {
        private readonly SKCanvasControl _owner;
        public GpuContextCapture(SKCanvasControl owner) => _owner = owner;

        public Rect Bounds => new Rect(0, 0, _owner.Bounds.Width, _owner.Bounds.Height);
        public bool HitTest(Point p) => false;
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            GRContext? grContext = null;
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature != null)
            {
                using var lease = leaseFeature.Lease();
                grContext = lease?.GrContext;
            }

            ShareX.ImageEditor.ImageEffects.Adjustments.ImageEffect.SetGpuContext(grContext);

            // Log state changes only — not every frame — so the developer can confirm the active backend
            bool hasGpu = grContext != null;
            if (hasGpu != _owner._lastRenderHadGpu)
            {
                Debug.WriteLine(hasGpu
                    ? "[SKCanvasControl] GPU backend detected — GRContext assigned to ImageEffect pipeline"
                    : "[SKCanvasControl] No GPU backend (software renderer) — ImageEffect pipeline will use CPU path");
                _owner._lastRenderHadGpu = hasGpu;
            }
        }
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
                    {
                        drawAction(surface.Canvas);
                    }
                }
            }
        }

        // Try to invalidate only if on UI thread, otherwise dispatcher?
        // Render method is called by UI thread. Draw might be called from Core.
        // We need to request invalidation on UI thread.
        Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual, Avalonia.Threading.DispatcherPriority.Render);
    }

    /// <summary>
    /// Releases resources
    /// </summary>
    public void Dispose()
    {
        _bitmap?.Dispose();
        _bitmap = null;
    }
}
