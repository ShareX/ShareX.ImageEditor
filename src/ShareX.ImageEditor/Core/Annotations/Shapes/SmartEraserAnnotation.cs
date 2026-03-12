namespace ShareX.ImageEditor.Core.Annotations;

/// <summary>
/// Smart Eraser annotation - samples pixel color from the rendered canvas (including other annotations)
/// at click point and uses it for drawing to hide sensitive information by covering it with the
/// sampled color from the visual output
/// </summary>
public class SmartEraserAnnotation : RectangleAnnotation
{
    public SmartEraserAnnotation()
    {
        ToolType = EditorTool.SmartEraser;
        // Default to a visible preview color until the canvas sample is available.
        StrokeColor = "#80FF0000";
        FillColor = "#80FF0000";
        StrokeWidth = 0;
        CornerRadius = 0;
        ShadowEnabled = false;
    }

    // StrokeColor/FillColor will be set to the sampled pixel color from the RENDERED canvas
    // (including all annotations) when the user first clicks with the Smart Eraser tool.
    // This allows users to cover sensitive information with colors that match
    // existing annotations or the background, effectively hiding it seamlessly.
}
