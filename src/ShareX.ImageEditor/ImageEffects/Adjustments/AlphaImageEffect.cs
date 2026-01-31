using SkiaSharp;


namespace ShareX.ImageEditor.ImageEffects.Adjustments;

public class AlphaImageEffect : ImageEffect
{
    public override string Name => "Alpha";
    public override string IconKey => "IconEyeDropper";
    public float Amount { get; set; } = 100f; // 0 to 100

    public override SKBitmap Apply(SKBitmap source)
    {
        float a = Amount / 100f;
        float[] matrix = {
            1, 0, 0, 0, 0,
            0, 1, 0, 0, 0,
            0, 0, 1, 0, 0,
            0, 0, 0, a, 0
        };
        return ApplyColorMatrix(source, matrix);
    }
}

