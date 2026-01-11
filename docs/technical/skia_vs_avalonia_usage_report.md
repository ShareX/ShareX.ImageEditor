# ShareX.Editor SkiaSharp vs Avalonia Usage Report

## 1. Scope and Entrypoints

The review focused on `ShareX.Editor`, an Avalonia-based image editor.

*   **Primary Entrypoint**: `ShareX.Editor.Views.EditorView` (Avalonia UserControl).
*   **Rendering Architecture**: The editor currently employs a **Hybrid** architecture.
    *   **On-Screen (Interactive)**: Performed primarily by Avalonia native controls (`Canvas`, `Shape`, `TextBox`, `Image`) orchestrated by `EditorView.axaml.cs`.
    *   **Off-Screen (Processing/Export)**: Performed by `SkiaSharp` directly via helper classes (`ImageEffects`) and `EditorCore`.
*   **Key Controls**:
    *   `PreviewImageControl` (Avalonia `Image`): Displays the base image.
    *   `AnnotationCanvas` (Avalonia `Canvas`): Hosts overlay elements (shapes, text).
    *   `OverlayCanvas` (Avalonia `Canvas`): Hosts transient UI elements like selection handles and crop/cutout overlays.

## 2. Current Implementation Analysis

The investigation reveals a distinct separation between "Visuals" (Avalonia) and "Model/Logic" (SkiaSharp).

### 2.1 The "Shadow" Engine (`EditorCore`)
The `EditorCore` class contains a full rendering loop and operation logic using SkiaSharp (`Render(SKCanvas)`).
*   **Recent Optimizations**: Methods like `PerformCutOut` have been optimized to use `SKCanvas` and `DrawBitmap` for high-performance pixel manipulation, replacing slow `GetPixel`/`SetPixel` loops.
*   **Role**: It serves as the "Backend" for image manipulations. It maintains the canonical `SKBitmap` of the source image.

### 2.2 The "Visual" Engine (`EditorView`)
The `EditorView` handles user interaction and displays the state using Avalonia primitives.
*   **Synchronization**: The View displays a converted version of the Backend's `SKBitmap`.
*   **Interactions**: resizing, moving, and drawing annotations are handled by Avalonia events and transforms on `Control` elements.

### 2.3 Integration Points
*   **Snapshotting**: `EditorView.GetSnapshot()` renders the visual tree to a `RenderTargetBitmap` (Avalonia) and converts it to `SKBitmap` (Skia) for saving/exporting.
*   **Effects**: Complex effects (Blur, Pixelate) utilize `ImageEffects` (Skia) to process the underlying bitmap.

## 3. Usage Classification

| Usage Type | Implementation | Locations |
| :--- | :--- | :--- |
| **Interactive UI** | Avalonia Controls | `EditorView.axaml`, `AnnotationCanvas`, Selection Handles |
| **Image Display** | Avalonia `Image` | `PreviewImageControl` |
| **Image Processing** | SkiaSharp | `EditorCore.cs`, `ImageEffects/*`, `BitmapConversionHelpers` |
| **Export/Save** | Avalonia RTB -> Skia | `EditorView.GetSnapshot()` |

## 4. Performance Enhancements & Recommendations

To achieve the best fluid, responsive user interaction and fastest image manipulations, the following architecture is recommended.

### 4.1 Hybrid Rendering Model (Best of Both Worlds)

The ideal approach leverages Avalonia for the "Chrome" and Event handling, and SkiaSharp for the "Content".

#### **Layer 1: High-Performance Image Surface (SkiaSharp)**
Instead of using a standard Avalonia `Image` control which requires `Bitmap` conversion, use a custom control backed by `SKCanvas` (e.g., via `Avalonia.Skia` integration or a `WriteableBitmap` drawn to by Skia).

*   **Why**: Zero-copy rendering. You draw the `SKBitmap` directly to the screen.
*   **Usage**: The background image, and "Rasterized" annotations (like Blur/Pixelate/Highlight) should be rendered here.
*   **Implementation**: Create an `SKCanvasControl` that wraps the `EditorCore.Render(SKCanvas)` method.

#### **Layer 2: Vector Overlay (Avalonia)**
Use Avalonia Controls for **Active** and **Vector** elements.

*   **Why**: Resolution independence, built-in hit testing, accessibility support, and perfect coordination with the rest of the application UI.
*   **Usage**:
    *   **Selection Handles**: Resizing grips, rotation handles.
    *   **Active Shape**: The shape currently being drawn.
    *   **Text Editing**: `TextBox` for text input (handling cursors/IME is hard in Skia).
    *   **Simple Shapes**: Rectangles, Arrows, Ellipses (until they are "baked" into the image).

### 4.2 Optimizing Image Manipulations (Cropping, Filters)

#### **Strategy: "Lazy" Rendering & Command Buffers**
For operations like Crop or Filter, do not immediately destroy the original pixels if possible.

1.  **Preview Mode (Fast)**:
    *   When the user drags a slider (e.g., Blur strength), do **not** re-render the full 4K image on every pixel move.
    *   Render a **Downsampled** version (low-res) or a **Tile** (viewport only) using Skia.
    *   Display this "dirty" preview on top of the image.

2.  **Commit Mode (High Quality)**:
    *   On `PointerReleased`, apply the expensive Skia filter to the full resolution `SKBitmap`.
    *   Update the `SKCanvasControl` to show the new state.

#### **Strategy: Tiled Rendering (for Zoom/Pan)**
If editing extremely large screenshots (e.g., 8K scrolling captures), rendering the whole bitmap every frame is slow.

*   **Implementation**: Divide the `SKBitmap` into tiles (e.g., 512x512). Only draw tiles visible in the current viewport. Skia handles this well `DrawBitmapRect`.

### 4.3 Proposed Architecture Diagram

```mermaid
graph TD
    UserInput["User Input (Mouse/Key)"] -->|Events| AvaloniaView["EditorView (Avalonia)"]
    
    subgraph "Visual Layer (Avalonia)"
        AvaloniaView -->|Updates| OverlayCanvas["Overlay Canvas"]
        OverlayCanvas -->|Contains| Handles["Selection Handles"]
        OverlayCanvas -->|Contains| ActiveShape["Active Drawing Shape"]
        ActiveShape -->|Vector Rendering| GPU["GPU Composition"]
    end

    subgraph "Content Layer (SkiaSharp)"
        AvaloniaView -->|Commands (Crop/Cut)| EditorCore["EditorCore"]
        EditorCore -->|Manipulates| MasterBitmap["Master SKBitmap"]
        MasterBitmap -->|Draws to| SKSurface["SKCanvasControl / WriteableBitmap"]
        SKSurface -->|Composites| AvaloniaView
    end

    EditorCore -->|Uses| PerformCutOut["Optimized PerformCutOut"]
    EditorCore -->|Uses| ImageEffects["Skia ImageEffects"]
```

### 4.4 Summary of Recommendations

1.  **Keep Vectors Vector**: Continue using Avalonia shapes for annotations. They are crisp and easy to manage.
2.  **Move Image Logic to Skia**: Ensure ALL pixel mutations (Crop, CutOut, Blur) happen in `EditorCore` using `SKCanvas`/`SKSurface`, **never** `GetPixel`.
3.  **Optimize the Bridge**: Minimize `Bitmap` <-> `SKBitmap` conversions. Use `WriteableBitmap.Lock()` to get a pointer that Skia can draw into directly, allowing real-time Skia rendering into an Avalonia Image source.
