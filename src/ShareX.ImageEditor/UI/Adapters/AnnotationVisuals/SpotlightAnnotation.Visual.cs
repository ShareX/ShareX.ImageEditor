using Avalonia.Controls;
using ShareX.ImageEditor.Controls;

namespace ShareX.ImageEditor.Annotations;

public partial class SpotlightAnnotation
{
    /// <summary>
    /// Creates the Avalonia visual for this annotation.
    /// </summary>
    public Control CreateVisual()
    {
        return new SpotlightControl
        {
            Annotation = this,
            IsHitTestVisible = false,
            Tag = this
        };
    }
}
