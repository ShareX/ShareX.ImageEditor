using SkiaSharp;


namespace ShareX.ImageEditor.ImageEffects.Adjustments;

public class BrightnessImageEffect : ImageEffect
{
    public override string Name => "Brightness";
    public override string IconKey => "IconSun";
    public float Amount { get; set; } = 0; // -100 to 100

    public override SKBitmap Apply(SKBitmap source)
    {
        float value = Amount / 100f;
        float[] matrix = {
            1, 0, 0, 0, value,
            0, 1, 0, 0, value,
            0, 0, 1, 0, value,
            0, 0, 0, 1, 0
        };
        return ApplyColorMatrix(source, matrix);
    }
}

