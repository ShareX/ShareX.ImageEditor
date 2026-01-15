using SkiaSharp;
using ShareX.Editor.Helpers;

namespace ShareX.Editor.ImageEffects;

public class ManipulationsRoundedCornersImageEffect : ImageEffect
{
    public override string Name => "Rounded Corners";
    public override ImageEffectCategory Category => ImageEffectCategory.Manipulations;
    public override bool HasParameters => true;
    public int CornerRadius { get; set; } = 20;

    public override SKBitmap Apply(SKBitmap source) 
    {
        return ImageHelpers.RoundedCorners(source, CornerRadius);
    }
}
