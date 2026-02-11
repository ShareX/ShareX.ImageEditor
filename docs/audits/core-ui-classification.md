# Core vs UI Classification

Date: 2026-02-11
Repository: `ShareX/ImageEditor`

## Legend
- **Core**: algorithm/state/serialization/rendering on SkiaSharp and pure C#
- **UI**: Avalonia views, controls, dialogs, UI-only services/adapters
- **Risk**:
  - `High`: undo/redo, EditorCore, annotations, effect stack
  - `Medium`: shared helpers and serialization
  - `Low`: folder move only (view/layout)

## Classification Map

| Current path | Proposed destination | Classification | Why | Risk |
|---|---|---|---|---|
| `src/ShareX.ImageEditor/EditorCore.cs` | `src/ShareX.ImageEditor/Core/Editor/EditorCore.cs` | Core | Editing engine, pointer state machine, render orchestration | High |
| `src/ShareX.ImageEditor/EditorOptions.cs` | `src/ShareX.ImageEditor/Core/Editor/EditorOptions.cs` | Core | Editor runtime options model | Medium |
| `src/ShareX.ImageEditor/EditorHistory.cs` | `src/ShareX.ImageEditor/Core/History/EditorHistory.cs` | Core | Undo/redo stack and mementos | High |
| `src/ShareX.ImageEditor/EditorMemento.cs` | `src/ShareX.ImageEditor/Core/History/EditorMemento.cs` | Core | Undo/redo snapshot state | High |
| `src/ShareX.ImageEditor/Annotations/Annotation.cs` | `src/ShareX.ImageEditor/Core/Annotations/Base/Annotation.cs` | Core | Base annotation state/render/hit-test contract | High |
| `src/ShareX.ImageEditor/Annotations/IPointBasedAnnotation.cs` | `src/ShareX.ImageEditor/Core/Annotations/Base/IPointBasedAnnotation.cs` | Core | Annotation abstraction for point-based tools | High |
| `src/ShareX.ImageEditor/Annotations/EditorTool.cs` | `src/ShareX.ImageEditor/Core/Editor/EditorTool.cs` | Core | Tool enum used by engine/history/toolbar state | High |
| `src/ShareX.ImageEditor/Annotations/BaseEffectAnnotation.cs` | `src/ShareX.ImageEditor/Core/Annotations/Effects/BaseEffectAnnotation.cs` | Core | Base class for effect annotations | High |
| `src/ShareX.ImageEditor/Annotations/RectangleAnnotation.cs` | `src/ShareX.ImageEditor/Core/Annotations/Shapes/RectangleAnnotation.cs` | Core | Shape annotation behavior | High |
| `src/ShareX.ImageEditor/Annotations/EllipseAnnotation.cs` | `src/ShareX.ImageEditor/Core/Annotations/Shapes/EllipseAnnotation.cs` | Core | Shape annotation behavior | High |
| `src/ShareX.ImageEditor/Annotations/LineAnnotation.cs` | `src/ShareX.ImageEditor/Core/Annotations/Shapes/LineAnnotation.cs` | Core | Shape annotation behavior | High |
| `src/ShareX.ImageEditor/Annotations/ArrowAnnotation.cs` | `src/ShareX.ImageEditor/Core/Annotations/Shapes/ArrowAnnotation.cs` | Core | Shape annotation behavior | High |
| `src/ShareX.ImageEditor/Annotations/FreehandAnnotation.cs` | `src/ShareX.ImageEditor/Core/Annotations/Shapes/FreehandAnnotation.cs` | Core | Point-based drawing behavior | High |
| `src/ShareX.ImageEditor/Annotations/SmartEraserAnnotation.cs` | `src/ShareX.ImageEditor/Core/Annotations/Shapes/SmartEraserAnnotation.cs` | Core | Smart eraser stroke behavior | High |
| `src/ShareX.ImageEditor/Annotations/TextAnnotation.cs` | `src/ShareX.ImageEditor/Core/Annotations/Text/TextAnnotation.cs` | Core | Text annotation model/render/hit-test | High |
| `src/ShareX.ImageEditor/Annotations/NumberAnnotation.cs` | `src/ShareX.ImageEditor/Core/Annotations/Text/NumberAnnotation.cs` | Core | Number annotation model/render/hit-test | High |
| `src/ShareX.ImageEditor/Annotations/SpeechBalloonAnnotation.cs` | `src/ShareX.ImageEditor/Core/Annotations/Text/SpeechBalloonAnnotation.cs` | Core | Balloon text annotation behavior | High |
| `src/ShareX.ImageEditor/Annotations/BlurAnnotation.cs` | `src/ShareX.ImageEditor/Core/Annotations/Effects/BlurAnnotation.cs` | Core | Effect annotation behavior | High |
| `src/ShareX.ImageEditor/Annotations/PixelateAnnotation.cs` | `src/ShareX.ImageEditor/Core/Annotations/Effects/PixelateAnnotation.cs` | Core | Effect annotation behavior | High |
| `src/ShareX.ImageEditor/Annotations/HighlightAnnotation.cs` | `src/ShareX.ImageEditor/Core/Annotations/Effects/HighlightAnnotation.cs` | Core | Effect annotation behavior | High |
| `src/ShareX.ImageEditor/Annotations/MagnifyAnnotation.cs` | `src/ShareX.ImageEditor/Core/Annotations/Effects/MagnifyAnnotation.cs` | Core | Effect annotation behavior | High |
| `src/ShareX.ImageEditor/Annotations/SpotlightAnnotation.cs` | `src/ShareX.ImageEditor/Core/Annotations/Effects/SpotlightAnnotation.cs` | Core | Effect annotation behavior | High |
| `src/ShareX.ImageEditor/Annotations/CropAnnotation.cs` | `src/ShareX.ImageEditor/Core/Annotations/Shapes/CropAnnotation.cs` | Core | Crop operation annotation | High |
| `src/ShareX.ImageEditor/Annotations/CutOutAnnotation.cs` | `src/ShareX.ImageEditor/Core/Annotations/Shapes/CutOutAnnotation.cs` | Core | CutOut operation annotation | High |
| `src/ShareX.ImageEditor/Annotations/ImageAnnotation.cs` | `src/ShareX.ImageEditor/Core/Annotations/Shapes/ImageAnnotation.cs` | Core | Embedded image annotation behavior | High |
| `src/ShareX.ImageEditor/Serialization/AnnotationSerializer.cs` | `src/ShareX.ImageEditor/Core/Serialization/AnnotationSerializer.cs` | Core | Core-safe annotation serialization | Medium |
| `src/ShareX.ImageEditor/ImageEffects/ImageEffect.cs` | `src/ShareX.ImageEditor/Core/ImageEffects/ImageEffect.cs` | Core | Effect base model contract | High |
| `src/ShareX.ImageEditor/ImageEffects/ImageEffectCategory.cs` | `src/ShareX.ImageEditor/Core/ImageEffects/ImageEffectCategory.cs` | Core | Effect category metadata | Medium |
| `src/ShareX.ImageEditor/ImageEffects/ImageEffectRegistry.cs` | `src/ShareX.ImageEditor/Core/ImageEffects/ImageEffectRegistry.cs` | Core | Effect registration/composition | High |
| `src/ShareX.ImageEditor/ImageEffects/Adjustments/*.cs` | `src/ShareX.ImageEditor/Core/ImageEffects/Adjustments/*.cs` | Core | SKBitmap adjustment effects | High |
| `src/ShareX.ImageEditor/ImageEffects/Filters/*.cs` | `src/ShareX.ImageEditor/Core/ImageEffects/Filters/*.cs` | Core | SKBitmap filter effects | High |
| `src/ShareX.ImageEditor/ImageEffects/Manipulations/*.cs` | `src/ShareX.ImageEditor/Core/ImageEffects/Manipulations/*.cs` | Core | SKBitmap manipulation effects | High |
| `src/ShareX.ImageEditor/Helpers/ImageHelpers.cs` | `src/ShareX.ImageEditor/Core/ImageEffects/Helpers/ImageHelpers.cs` | Core | Skia image helper methods | Medium |
| `src/ShareX.ImageEditor/Extensions/SkiaSharpConversions.cs` | `src/ShareX.ImageEditor/Core/ImageEffects/Helpers/SkiaSharpConversions.cs` | Core | Conversion helpers used by effects/engine | Medium |
| `src/ShareX.ImageEditor/Extensions/TypeExtensions.cs` | `src/ShareX.ImageEditor/Core/ImageEffects/Helpers/TypeExtensions.cs` | Core | Metadata helper used by effect lists | Low |
| `src/ShareX.ImageEditor/Views/EditorView.axaml*` | `src/ShareX.ImageEditor/UI/Views/EditorView.axaml*` | UI | Main Avalonia editor view | Low |
| `src/ShareX.ImageEditor/Views/EditorWindow.axaml*` | `src/ShareX.ImageEditor/UI/Views/EditorWindow.axaml*` | UI | Host window for editor view | Low |
| `src/ShareX.ImageEditor/Views/Controls/EditorCanvas.cs` | `src/ShareX.ImageEditor/UI/Controls/EditorCanvas.cs` | UI | Avalonia canvas host control | Medium |
| `src/ShareX.ImageEditor/Views/Controllers/*.cs` | `src/ShareX.ImageEditor/UI/Adapters/*.cs` | UI | Input/selection/zoom view controllers | Medium |
| `src/ShareX.ImageEditor/Views/Dialogs/*.axaml*` | `src/ShareX.ImageEditor/UI/Views/Dialogs/*.axaml*` | UI | Avalonia effect/dialog UIs | Low |
| `src/ShareX.ImageEditor/ViewModels/*.cs` | `src/ShareX.ImageEditor/UI/ViewModels/*.cs` | UI | Avalonia-facing VM layer | Medium |
| `src/ShareX.ImageEditor/Controls/*.axaml*` | `src/ShareX.ImageEditor/UI/Controls/*.axaml*` | UI | Avalonia reusable controls | Low |
| `src/ShareX.ImageEditor/Converters/*.cs` | `src/ShareX.ImageEditor/UI/Adapters/Converters/*.cs` | UI | Avalonia value converters | Low |
| `src/ShareX.ImageEditor/Services/IClipboardService.cs` | `src/ShareX.ImageEditor/UI/Adapters/IClipboardService.cs` | UI | Clipboard is UI host concern | Medium |
| `src/ShareX.ImageEditor/Helpers/BitmapConversionHelpers.cs` | `src/ShareX.ImageEditor/UI/Adapters/BitmapConversionHelpers.cs` | UI | Avalonia bitmap conversion bridge | Medium |
| `src/ShareX.ImageEditor/Helpers/FontAwesomeIcons.cs` | `src/ShareX.ImageEditor/UI/Controls/FontAwesomeIcons.cs` | UI | UI icon constant surface | Low |
| `src/ShareX.ImageEditor/Helpers/DebugHelper.cs` | `src/ShareX.ImageEditor/Core/Editor/DebugHelper.cs` | Core | Non-UI diagnostics helper | Low |
| `src/ShareX.ImageEditor/EditorHostExample.cs` | `src/ShareX.ImageEditor/UI/Adapters/EditorHostExample.cs` | UI | Demo host integration surface | Low |

## Special Notes
- Annotation types currently include Avalonia-specific `CreateVisual()` members in several classes. Those members will be shifted to `UI/Adapters/AnnotationVisualFactory` or equivalent adapter-facing partials while preserving behavior and compatibility.
- Undo/redo (`EditorHistory`, `EditorMemento`) and engine (`EditorCore`) are designated **high-risk no-regression** areas and will be moved first with build checkpoints.
