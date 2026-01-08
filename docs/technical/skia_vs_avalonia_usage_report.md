# ShareX.Editor SkiaSharp vs Avalonia Usage Report

## 1. Scope and Entrypoints

The review focused on `ShareX.Editor`, an Avalonia-based image editor.

*   **Primary Entrypoint**: `ShareX.Editor.Views.EditorView` (Avalonia UserControl).
*   **Rendering Architecture**: The editor currently employs a **Hybrid/Split** architecture.
    *   **On-Screen (Interactive)**: Performed primarily by Avalonia native controls (`Canvas`, `Shape`, `TextBox`, `Image`) orchestrated by `EditorView.axaml.cs`.
    *   **Off-Screen (Processing/Export)**: Performed by `SkiaSharp` directly via helper classes (`ImageEffects`) and potentially `EditorCore` (though the latter's integration appears incomplete).
*   **Key Controls**:
    *   `PreviewImageControl` (Avalonia `Image`): Displays the base image.
    *   `AnnotationCanvas` (Avalonia `Canvas`): Hosts overlay elements.
    *   `SpotlightControl` (Custom Avalonia `Control`): Renders darkness overlay with a "hole" using Avalonia `DrawingContext`.
    *   `SpeechBalloonControl` (Custom Avalonia `Control`): Renders balloon geometry using Avalonia `StreamGeometry`.

## 2. Findings Summary

The investigation reveals a distinct separation between "Visuals" (Avalonia) and "Rendering" (SkiaSharp), with some code duplication and unused paths.

*   **EditorCore.cs is largely a "Shadow" engine**: The `EditorCore` class contains a full input handling and rendering loop using SkiaSharp (`Render(SKCanvas)`). However, this logic is **bypassed** by the active UI in `EditorView.axaml.cs`, which implements its own input handling and rendering using Avalonia primitives.
*   **Annotations have Dual-Identity**: Annotation classes (e.g., `BlurAnnotation`, `RectangleAnnotation`) possess two distinct visual implementations:
    *   `CreateVisual()`: Returns an Avalonia `Control` for the UI (often a simplified placeholder, e.g., a colored transparent box for Blur).
    *   `Render(SKCanvas)`: Contains the "true" drawing logic (e.g., actual Gaussian blur via Skia). This rendering path is currently **unused** for on-screen display in the current Editor View.
*   **ImageEffects are Active Skia Consumers**: The `ImageEffects` namespace (Filters, Manipulations) contains heavy code-level usage of SkiaSharp, which is actively used by the `MainViewModel` to apply effects to the underlying image bitmap.
*   **Project Coupling**: Despite claims of being "UI-agnostic" in `EditorCore.cs` comments, the `ShareX.Editor` project directly references `Avalonia` packages and imports Avalonia namespaces in its core Annotation classes to support `CreateVisual()`.

## 3. Usage Classification (Buckets)

### Bucket A: Direct SkiaSharp on-screen rendering
*   **Count**: 0 Call Sites.
*   **Details**: No active direct SkiaSharp rendering (e.g., `SKCanvasView.PaintSurface`) was found driving the main editor canvas. The `EditorCore.Render` method exists but is not hooked into the active `EditorView`.

### Bucket B: Direct SkiaSharp off-screen image processing
*   **Count**: High (~50 files).
*   **Key Locations**: 
    *   `src/ShareX.Editor/ImageEffects/*` (Filters, Manipulations, Drawings).
    *   `src/ShareX.Editor/Helpers/BitmapConversionHelpers.cs` (Conversion logic).
    *   `src/ShareX.Editor/Annotations/Annotation.cs` (and subclasses) - `Render` method logic (even if unused for screen, it constitutes logic for export/processing).
    *   `src/ShareX.Editor/EditorCore.cs`.

### Bucket C: Avalonia drawing APIs rendered via Avalonia
*   **Count**: Moderate (~10 files, high complexity).
*   **Key Locations**:
    *   `src/ShareX.Editor/Views/EditorView.axaml.cs` (Main canvas orchestration).
    *   `src/ShareX.Editor/Controls/SpotlightControl.cs`.
    *   `src/ShareX.Editor/Controls/SpeechBalloonControl.cs`.
    *   `src/ShareX.Editor/Annotations/*.cs` (`CreateVisual` methods).

### Bucket D: Mixed/Interop paths
*   **Key Locations**:
    *   `src/ShareX.Editor/ViewModels/MainViewModel.cs` calls `BitmapConversionHelpers` to convert between Avalonia `Bitmap` and `SKBitmap` to apply Skia-based effects.
    *   `src/ShareX.Editor/Views/EditorView.axaml.cs` uses `RenderTargetBitmap` (Avalonia) but relies on `BitmapConversionHelpers` (Skia) for clipboard operations.

## 4. Quantification

| Metric | Count | Notes |
| :--- | :--- | :--- |
| **Direct SkiaSharp Referencing Files** | ~70 files | Dominates logic in `ImageEffects` and `Annotations`. |
| **Avalonia Graphics Referencing Files** | ~25 files | `EditorView`, `Controls`, `Annotations` (for `CreateVisual`). |
| **Custom Render Overrides (Avalonia)** | 2 | `SpotlightControl`, `SpeechBalloonControl`. |
| **Direct Skia Render methods** | 19 | One per Annotation subclass + `EditorCore`. (Mostly unused shadow code). |
| **Avalonia Visual Creators** | 19 | One per Annotation subclass (`CreateVisual`). |

**Dominant Rendering Approach for Annotations**:
*   **Interactive (Screen)**: Avalonia Native (Shapes/Controls).
*   **Processing (Logic)**: SkiaSharp (Shadow implementation).

## 5. Backend Coupling Risks

*   **Avalonia.Skia Dependency**: The project does **not** explicitly reference `Avalonia.Skia`. This means `ShareX.Editor` is theoretically backend-agnostic regarding Avalonia's rendering pipeline.
*   **SkiaSharp Library Dependency**: The project has a hard dependency on `SkiaSharp` (the library) for its internal image processing logic (`ImageEffects`). This is independent of the Avalonia rendering backend.
*   **Performance Risk**: The reliance on `BitmapConversionHelpers` to shuttle property data between Avalonia `Bitmap` and `SKBitmap` for effects (and potentially for future high-fidelity rendering) incurs a memory copy cost.
*   **Refactoring Risk**: The logic split between `CreateVisual()` (Avalonia) and `Render()` (Skia) in generic annotation classes suggests technical debt. The "UI-agnostic" core is currently coupled to Avalonia, preventing it from being easily ported to WinForms (likely the original intent of `EditorCore`) without separating `CreateVisual` out.

## Appendix: Search Terms Used
*   `using SkiaSharp;`
*   `SKCanvas`, `SKBitmap`, `SKPaint`
*   `DrawingContext`
*   `EditorCore`
