#region License Information (GPL v3)

/*
    ShareX.Editor - The UI-agnostic Editor library for ShareX
    Copyright (c) 2007-2025 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using ShareX.Editor.Annotations;
using SkiaSharp;

namespace ShareX.Editor;

/// <summary>
/// Manages undo/redo history for the editor using the Memento pattern.
/// Adapted from ShareX's ImageEditorHistory implementation.
/// </summary>
internal class EditorHistory : IDisposable
{
    public bool CanUndo => _undoMementoStack.Count > 0;
    public bool CanRedo => _redoMementoStack.Count > 0;

    /// <summary>
    /// Maximum number of canvas mementos (destructive operations) to keep.
    /// ISSUE-003 mitigation: Canvas mementos contain full bitmap copies and can consume
    /// significant memory (e.g., 8MB per 4K screenshot). Limiting to 5 balances undo depth with memory.
    /// </summary>
    private const int MaxCanvasMementos = 5;

    /// <summary>
    /// Maximum number of annotation-only mementos to keep (lightweight operations).
    /// These don't store canvas bitmaps, so we can keep more.
    /// </summary>
    private const int MaxAnnotationMementos = 20;

    private readonly EditorCore _editorCore;
    private readonly Stack<EditorMemento> _undoMementoStack = new();
    private readonly Stack<EditorMemento> _redoMementoStack = new();

    public EditorHistory(EditorCore editorCore)
    {
        _editorCore = editorCore;
    }

    /// <summary>
    /// Add a memento to the undo stack and clear redo stack
    /// </summary>
    private void AddMemento(EditorMemento memento)
    {
        _undoMementoStack.Push(memento);

        // ISSUE-003 mitigation: Limit stack depth based on memento type
        int maxDepth = memento.Canvas != null ? MaxCanvasMementos : MaxAnnotationMementos;

        // Remove oldest mementos if exceeding limit
        if (_undoMementoStack.Count > maxDepth)
        {
            var tempStack = new Stack<EditorMemento>();

            // Keep most recent N mementos
            for (int i = 0; i < maxDepth; i++)
            {
                tempStack.Push(_undoMementoStack.Pop());
            }

            // Dispose excess old mementos
            while (_undoMementoStack.Count > 0)
            {
                var oldMemento = _undoMementoStack.Pop();
                oldMemento?.Dispose();
                System.Diagnostics.Debug.WriteLine($"[HISTORY] Disposed old memento (Canvas: {oldMemento?.Canvas != null}, Stack limit: {maxDepth})");
            }

            // Restore kept mementos
            while (tempStack.Count > 0)
            {
                _undoMementoStack.Push(tempStack.Pop());
            }
        }

        // Clear redo stack when new action is performed
        foreach (EditorMemento redoMemento in _redoMementoStack)
        {
            redoMemento?.Dispose();
        }

        _redoMementoStack.Clear();
    }

    /// <summary>
    /// Create a memento with full canvas bitmap (for destructive operations like crop/cutout)
    /// </summary>
    private EditorMemento GetMementoFromCanvas()
    {
        List<Annotation> annotations = _editorCore.GetAnnotationsSnapshot();
        SKBitmap? canvas = _editorCore.SourceImage?.Copy();
        return new EditorMemento(annotations, _editorCore.CanvasSize, canvas);
    }

    /// <summary>
    /// Create a memento with only annotations (for non-destructive annotation operations)
    /// </summary>
    private EditorMemento GetMementoFromAnnotations(Annotation? excludeAnnotation = null)
    {
        List<Annotation> annotations = _editorCore.GetAnnotationsSnapshot(excludeAnnotation);
        return new EditorMemento(annotations, _editorCore.CanvasSize);
    }

    /// <summary>
    /// Create a canvas memento before destructive operations (crop, cutout)
    /// </summary>
    public void CreateCanvasMemento()
    {
        EditorMemento memento = GetMementoFromCanvas();

        // ISSUE-003 mitigation: Log memory usage for large canvas mementos
        if (memento.Canvas != null)
        {
            long bitmapBytes = (long)memento.Canvas.Info.BytesSize;
            double bitmapMB = bitmapBytes / (1024.0 * 1024.0);

            System.Diagnostics.Debug.WriteLine($"[HISTORY] Canvas memento created: {memento.Canvas.Width}x{memento.Canvas.Height}, {bitmapMB:F2} MB");

            // Warn if canvas is very large (> 10MB)
            if (bitmapMB > 10.0)
            {
                System.Diagnostics.Debug.WriteLine($"[HISTORY] Warning: Large canvas memento ({bitmapMB:F2} MB) may impact memory usage. Consider limiting undo depth for large images.");
            }
        }

        AddMemento(memento);
    }

    /// <summary>
    /// Create an annotations-only memento for non-destructive operations.
    /// Excludes region tools (crop, cutout, spotlight) from memento creation.
    /// </summary>
    /// <param name="excludeAnnotation">Optional annotation to exclude from the memento (to capture state before it was added)</param>
    public void CreateAnnotationsMemento(Annotation? excludeAnnotation = null)
    {
        // Skip memento creation for region tools (crop, cutout, spotlight)
        // These tools execute immediately and don't need annotation history
        if (_editorCore.ActiveTool == EditorTool.Crop ||
            _editorCore.ActiveTool == EditorTool.Spotlight)
        {
            return;
        }

        EditorMemento memento = GetMementoFromAnnotations(excludeAnnotation);
        AddMemento(memento);
    }

    /// <summary>
    /// Undo the last operation
    /// </summary>
    public void Undo()
    {
        if (!CanUndo) return;

        EditorMemento undoMemento = _undoMementoStack.Pop();

        if (undoMemento.Annotations != null)
        {
            if (undoMemento.Canvas == null)
            {
                // Annotations-only undo: save current annotations to redo stack
                EditorMemento redoMemento = GetMementoFromAnnotations();
                _redoMementoStack.Push(redoMemento);

                _editorCore.RestoreState(undoMemento);
            }
            else
            {
                // Canvas undo: save current full state to redo stack
                EditorMemento redoMemento = GetMementoFromCanvas();
                _redoMementoStack.Push(redoMemento);

                _editorCore.RestoreState(undoMemento);
            }
        }
    }

    /// <summary>
    /// Redo the last undone operation
    /// </summary>
    public void Redo()
    {
        if (!CanRedo) return;

        EditorMemento redoMemento = _redoMementoStack.Pop();

        if (redoMemento.Annotations != null)
        {
            if (redoMemento.Canvas == null)
            {
                // Annotations-only redo: save current annotations to undo stack
                EditorMemento undoMemento = GetMementoFromAnnotations();
                _undoMementoStack.Push(undoMemento);

                _editorCore.RestoreState(redoMemento);
            }
            else
            {
                // Canvas redo: save current full state to undo stack
                EditorMemento undoMemento = GetMementoFromCanvas();
                _undoMementoStack.Push(undoMemento);

                _editorCore.RestoreState(redoMemento);
            }
        }
    }

    /// <summary>
    /// Clear all history and dispose resources
    /// </summary>
    public void Dispose()
    {
        foreach (EditorMemento undoMemento in _undoMementoStack)
        {
            undoMemento?.Dispose();
        }

        _undoMementoStack.Clear();

        foreach (EditorMemento redoMemento in _redoMementoStack)
        {
            redoMemento?.Dispose();
        }

        _redoMementoStack.Clear();
    }
}