using SkiaSharp;

namespace ShareX.ImageEditor.Services;

/// <summary>
/// Provides per-call access to the GPU rendering context for off-screen effect processing.
/// The implementation (in the Avalonia host) acquires a scoped lock on
/// <c>ISkiaGpuWithPlatformGraphicsContext.TryGetGrContext()</c> before invoking the delegate,
/// making the GL/Vulkan context current on the calling thread for the duration of the call.
/// </summary>
public interface IEffectGpuLeaseProvider
{
    /// <summary>
    /// Tries to acquire the GPU context and execute <paramref name="gpuWork"/> on it.
    /// Returns the bitmap produced by <paramref name="gpuWork"/>, or <c>null</c> if the
    /// context is unavailable or the work returns <c>null</c> (caller should fall back to CPU).
    /// </summary>
    SKBitmap? TryWithGrContext(Func<GRContext, SKBitmap?> gpuWork);
}
