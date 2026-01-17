# ShareX.Editor

**ShareX.Editor** is a powerful, cross-platform image editor component designed for the ShareX ecosystem. It provides a rich set of annotation tools, image manipulation effects, and a modern, responsive user interface built with Avalonia UI.

## 🚀 Features

### ✏️ Annotation Tools
A comprehensive suite of tools for markup and editing:
*   **Shapes**: Rectangle, Ellipse, Line, Arrow.
*   **Drawing**: Freehand (Pen) with smooth path rendering.
*   **Text & Stickers**: Text boxes, Speech Balloons, and Step Numbering.
*   **Highlighting**: Translucent Highlighter, Spotlight (dim background), and Magnifier.
*   **Editing**: Smart Eraser (content-aware), Cut Out (remove region), and Image Insertion.

### 🎨 Image Effects & Manipulation
Organized into three main categories for enhanced control:

*   **Adjustments**: Fine-tune image properties including Brightness, Contrast, Gamma, Hue/Saturation, and Curated Color filters (Sepia, Grayscale, Polaroid).
*   **Filters**: Apply artistic effects such as Blur (Box/Gaussian), Pixelate, Sharpen, Torn Edge, Shadow, and Reflection.
*   **Manipulations**: Transform the canvas with Resize, Rotate, Flip, Skew, and Auto-Crop operations.

### 🖱️ User Experience
*   **Modern UI**: Built on Avalonia 11 with a fluent, dark-themed design.
*   **Interactivity**: Pan and Zoom support, multi-level Undo/Redo.
*   **Control**: Customizable stroke colors (with palette), stroke widths, and opacity.

## 🛠️ Technology Stack

*   **[Avalonia UI](https://avaloniaui.net/)** (v11.x) - The cross-platform UI framework used for the editor's visual shell and interactive canvas.
*   **[SkiaSharp](https://github.com/mono/SkiaSharp)** (v2.88.x) - Used for high-performance image processing, filter application, and off-screen rendering tasks.
*   **[CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)** - Provides the MVVM architectural backbone (ObservableObject, RelayCommand).

## 🏗️ Architecture

ShareX.Editor employs a **Hybrid Rendering** architecture to balance performance and flexibility:

1.  **Interactive Layer (Avalonia)**: The editing surface (`EditorView`) utilizes Avalonia's native vector graphics and controls for fluid, responsive user interaction. Annotations are represented as Avalonia `Control`s or `Shape`s, allowing for styling, hit-testing, and event handling managed directly by the UI framework.
2.  **Processing Layer (SkiaSharp)**: Underlying image manipulations (such as cropping and applying filters) are handled directly by SkiaSharp bitmaps. This ensures high-fidelity output and efficient processing of pixel data.

## 📂 Project Structure

*   **ShareX.Editor**: The core library containing the editor logic and UI components.
    *   `Annotations`: Logic for individual tools (shapes, text, etc.).
    *   `ImageEffects`: Image processing logic split into Adjustments, Filters, and Manipulations.
    *   `Views`: Avalonia UserControls for the editor interface.
*   **ShareX.Editor.Loader**: A standalone executable for testing and running the editor during development.

## 📦 Integration

The editor is designed to be easily hosted within Avalonia applications.

```csharp
// Example usage in an Avalonia View
<UserControl xmlns:editor="using:ShareX.Editor.Views" ...>
    <editor:EditorView />
</UserControl>
```

The `MainViewModel` serves as the primary integration point for controlling the editor state (loading images, setting tools, saving output).
