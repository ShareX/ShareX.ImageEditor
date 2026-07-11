using Avalonia.Controls;
using ShareX.ImageEditor.Presentation.Controls;

namespace ShareX.ImageEditor.Core.Annotations;

public partial class SpeechBalloonAnnotation
{
    /// <summary>
    /// Creates the Avalonia visual for this annotation (SpeechBalloonControl)
    /// </summary>
    public Control CreateVisual()
    {
        var control = new SpeechBalloonControl
        {
            Annotation = this,
            IsHitTestVisible = true,
            Tag = this
        };

        if (ShadowEnabled)
        {
            control.Effect = ShareX.ImageEditor.Presentation.Helpers.ShadowEffectHelper.CreateDropShadow(this);
        }

        return control;
    }
}
