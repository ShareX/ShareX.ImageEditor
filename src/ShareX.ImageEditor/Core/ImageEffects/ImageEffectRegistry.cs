using ShareX.ImageEditor.ImageEffects.Adjustments;
using ShareX.ImageEditor.ImageEffects.Filters;
using ShareX.ImageEditor.ImageEffects.Manipulations;

namespace ShareX.ImageEditor.ImageEffects;

public static class ImageEffectRegistry
{
    public static IReadOnlyList<ImageEffect> Effects { get; }

    static ImageEffectRegistry()
    {
        var effects = new List<ImageEffect>
        {
            // Manipulations - Rotate
            RotateImageEffect.Clockwise90,
            RotateImageEffect.CounterClockwise90,
            RotateImageEffect.Rotate180,
            
            // Manipulations - Flip
            FlipImageEffect.Horizontal,
            FlipImageEffect.Vertical,
            new FlipImageEffect(),
            
            // Manipulations - Resize (parameterized)
            new ResizeImageEffect(),
            new ScaleImageEffect(),
            new AutoCropImageEffect(),
            new Rotate3DBoxImageEffect(),
            
            // Adjustments
            new BrightnessImageEffect(),
            new ContrastImageEffect(),
            new HueImageEffect(),
            new SaturationImageEffect(),
            new GammaImageEffect(),
            new AlphaImageEffect(),
            new ColorMatrixImageEffect(),
            new ColorizeImageEffect(),
            new SelectiveColorImageEffect(),
            new ReplaceColorImageEffect(),
            
            // Filters
            new ColorDepthImageEffect(),
            new ConvolutionMatrixImageEffect(),
            new EdgeDetectImageEffect(),
            new EmbossImageEffect(),
            new GaussianBlurImageEffect(),
            new MeanRemovalImageEffect(),
            new RGBSplitImageEffect(),
            new SmoothImageEffect(),
            new BlurImageEffect(),
            new PixelateImageEffect(),
            new SharpenImageEffect(),
            new WaveEdgeImageEffect(),
            
            // Adjustments - Color Filters
            new InvertImageEffect(),
            new GrayscaleImageEffect(),
            new BlackAndWhiteImageEffect(),
            new SepiaImageEffect(),
            new PolaroidImageEffect()
        };

        Effects = effects.AsReadOnly();
    }

    public static IEnumerable<ImageEffect> GetByCategory(ImageEffectCategory category)
    {
        return Effects.Where(e => e.Category == category);
    }
}
