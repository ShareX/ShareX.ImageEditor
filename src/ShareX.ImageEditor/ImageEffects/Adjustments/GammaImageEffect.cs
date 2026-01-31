using SkiaSharp;


namespace ShareX.ImageEditor.ImageEffects.Adjustments;

public class GammaImageEffect : ImageEffect
{
    public override string Name => "Gamma";
    public override string IconKey => "IconWaveSquare";
    public float Amount { get; set; } = 1f;

    public override SKBitmap Apply(SKBitmap source)
    {
        byte[] table = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            float val = i / 255f;
            float corrected = (float)Math.Pow(val, 1.0 / Amount);
            table[i] = (byte)(Math.Max(0, Math.Min(1, corrected)) * 255);
        }

        using var filter = SKColorFilter.CreateTable(null, table, table, table);
        return ApplyColorFilter(source, filter);
    }
}

