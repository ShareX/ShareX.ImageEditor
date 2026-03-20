using ShareX.ImageEditor.Core.ImageEffects.Helpers;
using SkiaSharp;

namespace ShareX.ImageEditor.Core.ImageEffects.Filters;

public class PaperStencilMaskImageEffect : FilterImageEffect
{
    public override string Name => "Paper stencil mask";
    public override bool HasParameters => true;

    public float Threshold { get; set; } = 140f; // 0..255
    public float FeatherRadius { get; set; } = 8f; // 0..200 heuristic
    public float EdgeStrength { get; set; } = 70f; // 0..100
    public float BackgroundDim { get; set; } = 35f; // 0..100
    public bool InvertMask { get; set; } = false;
    public int Seed { get; set; } = 1337;
    public SKColor StencilColor { get; set; } = new SKColor(0, 0, 0, 220);

    public override SKBitmap Apply(SKBitmap source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        float threshold01 = Math.Clamp(Threshold, 0f, 255f) / 255f;
        float featherPx = Math.Clamp(FeatherRadius, 0f, 200f);
        float feather = Math.Clamp(featherPx, 0f, 30f) / 30f; // 0..1
        float featherRange = 0.02f + 0.13f * feather; // luminance range around threshold

        float edgeStrength01 = Math.Clamp(EdgeStrength, 0f, 100f) / 100f;
        if (edgeStrength01 <= 0f && BackgroundDim <= 0f)
        {
            return source.Copy();
        }

        float bgDim01 = Math.Clamp(BackgroundDim, 0f, 100f) / 100f;
        float dimFactor = 1f - bgDim01;
        dimFactor = ProceduralEffectHelper.Clamp01(dimFactor);

        float stencilMixCap = (StencilColor.Alpha / 255f) * edgeStrength01;
        float stencilR = StencilColor.Red / 255f;
        float stencilG = StencilColor.Green / 255f;
        float stencilB = StencilColor.Blue / 255f;

        int width = source.Width;
        int height = source.Height;
        if (width <= 0 || height <= 0)
        {
            return source.Copy();
        }

        SKColor[] srcPixels = source.Pixels;
        SKColor[] dstPixels = new SKColor[srcPixels.Length];

        Parallel.For(0, height, y =>
        {
            int row = y * width;

            for (int x = 0; x < width; x++)
            {
                int idx = row + x;
                SKColor src = srcPixels[idx];

                float lum01 = ((0.2126f * src.Red) + (0.7152f * src.Green) + (0.0722f * src.Blue)) / 255f;

                // Optional tiny deterministic jitter to avoid banding at large feathers.
                float jitter = (ProceduralEffectHelper.Hash01(x, y, Seed ^ 0xCAFE) * 2f - 1f) * 0.003f;
                float t0 = threshold01 - featherRange + jitter;
                float t1 = threshold01 + featherRange + jitter;

                float maskAlpha = ProceduralEffectHelper.SmoothStep(t0, t1, lum01);
                if (InvertMask)
                {
                    maskAlpha = 1f - maskAlpha;
                }

                float stencilMix = maskAlpha * stencilMixCap;
                stencilMix = ProceduralEffectHelper.Clamp01(stencilMix);

                float outR = (src.Red / 255f) * dimFactor;
                float outG = (src.Green / 255f) * dimFactor;
                float outB = (src.Blue / 255f) * dimFactor;

                outR = ProceduralEffectHelper.Lerp(outR, stencilR, stencilMix);
                outG = ProceduralEffectHelper.Lerp(outG, stencilG, stencilMix);
                outB = ProceduralEffectHelper.Lerp(outB, stencilB, stencilMix);

                dstPixels[idx] = new SKColor(
                    ProceduralEffectHelper.ClampToByte(outR * 255f),
                    ProceduralEffectHelper.ClampToByte(outG * 255f),
                    ProceduralEffectHelper.ClampToByte(outB * 255f),
                    src.Alpha);
            }
        });

        return new SKBitmap(width, height, source.ColorType, source.AlphaType)
        {
            Pixels = dstPixels
        };
    }
}

