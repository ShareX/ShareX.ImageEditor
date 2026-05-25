#region License Information (GPL v3)

/*
    ShareX.ImageEditor - The UI-agnostic Editor library for ShareX
    Copyright (c) 2007-2026 ShareX Team

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

using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using ShareX.ImageEditor.Core.Annotations;
using ShareX.ImageEditor.Presentation.Controls;
using ShareX.ImageEditor.Presentation.Rendering;
using ShareX.ImageEditor.Presentation.ViewModels;

namespace ShareX.ImageEditor.Presentation.Views
{
    public partial class EditorView : UserControl
    {
        private void HookAnnotationToolbarEvents()
        {
            var toolbar = this.FindControl<AnnotationToolbar>("AnnotationToolbarControl");
            if (toolbar == null)
            {
                return;
            }

            toolbar.ColorChanged += OnColorChanged;
            toolbar.FillColorChanged += OnFillColorChanged;
            toolbar.TextColorChanged += OnTextColorChanged;
            toolbar.WidthChanged += OnWidthChanged;
            toolbar.CornerRadiusChanged += OnCornerRadiusChanged;
            toolbar.FontSizeChanged += OnFontSizeChanged;
            toolbar.FontFamilyChanged += OnFontFamilyChanged;
            toolbar.ArrowStyleChanged += OnArrowStyleChanged;
            toolbar.CursorTypeChanged += OnCursorTypeChanged;
            toolbar.StrengthChanged += OnStrengthChanged;
            toolbar.TextBoldChanged += OnToolbarTextBoldChanged;
            toolbar.TextItalicChanged += OnToolbarTextItalicChanged;
            toolbar.TextUnderlineChanged += OnToolbarTextUnderlineChanged;
            toolbar.ShadowChanged += OnToolbarShadowChanged;
        }

        private void UnhookAnnotationToolbarEvents()
        {
            var toolbar = this.FindControl<AnnotationToolbar>("AnnotationToolbarControl");
            if (toolbar == null)
            {
                return;
            }

            toolbar.ColorChanged -= OnColorChanged;
            toolbar.FillColorChanged -= OnFillColorChanged;
            toolbar.TextColorChanged -= OnTextColorChanged;
            toolbar.WidthChanged -= OnWidthChanged;
            toolbar.CornerRadiusChanged -= OnCornerRadiusChanged;
            toolbar.FontSizeChanged -= OnFontSizeChanged;
            toolbar.FontFamilyChanged -= OnFontFamilyChanged;
            toolbar.ArrowStyleChanged -= OnArrowStyleChanged;
            toolbar.CursorTypeChanged -= OnCursorTypeChanged;
            toolbar.StrengthChanged -= OnStrengthChanged;
            toolbar.TextBoldChanged -= OnToolbarTextBoldChanged;
            toolbar.TextItalicChanged -= OnToolbarTextItalicChanged;
            toolbar.TextUnderlineChanged -= OnToolbarTextUnderlineChanged;
            toolbar.ShadowChanged -= OnToolbarShadowChanged;
        }

        private void OnColorChanged(object? sender, IBrush color)
        {
            if (DataContext is MainViewModel vm && color is SolidColorBrush solidBrush)
            {
                var hexColor = $"#{solidBrush.Color.A:X2}{solidBrush.Color.R:X2}{solidBrush.Color.G:X2}{solidBrush.Color.B:X2}";
                vm.SetColorCommand.Execute(hexColor);
            }
        }

        private void OnFillColorChanged(object? sender, IBrush color)
        {
            if (DataContext is MainViewModel vm && color is SolidColorBrush solidBrush)
            {
                var hexColor = $"#{solidBrush.Color.A:X2}{solidBrush.Color.R:X2}{solidBrush.Color.G:X2}{solidBrush.Color.B:X2}";
                vm.FillColor = hexColor;
            }
        }

        private void OnTextColorChanged(object? sender, IBrush color)
        {
            if (DataContext is MainViewModel vm && color is SolidColorBrush solidBrush)
            {
                var hexColor = $"#{solidBrush.Color.A:X2}{solidBrush.Color.R:X2}{solidBrush.Color.G:X2}{solidBrush.Color.B:X2}";
                vm.TextColor = hexColor;
            }
        }

        private void OnFontSizeChanged(object? sender, float fontSize)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.FontSize = fontSize;
                ApplySelectedFontSize(fontSize);
            }
        }

        private void OnFontFamilyChanged(object? sender, string fontFamily)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SelectedFontFamily = fontFamily;
                ApplySelectedFontFamily(fontFamily);
            }
        }

        private void OnArrowStyleChanged(object? sender, ArrowStyle arrowStyle)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SelectedArrowStyle = arrowStyle;
                ApplySelectedArrowStyle(arrowStyle);
            }
        }

        private void OnCursorTypeChanged(object? sender, CursorType cursorType)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SelectedCursorType = cursorType;
                ApplySelectedCursorType(cursorType);
            }
        }

        private void OnStrengthChanged(object? sender, float strength)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.EffectStrength = strength;
                ApplySelectedEffectStrength(strength);
            }
        }

        private void OnShadowButtonClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.ShadowEnabled = !vm.ShadowEnabled;
                ApplySelectedShadowState(vm.ShadowEnabled);
            }
        }
        private void OnBoldButtonClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.TextBold = !vm.TextBold;
                ApplySelectedTextBold(vm.TextBold);
            }
        }

        private void OnItalicButtonClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.TextItalic = !vm.TextItalic;
                ApplySelectedTextItalic(vm.TextItalic);
            }
        }

        private void OnUnderlineButtonClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.TextUnderline = !vm.TextUnderline;
                ApplySelectedTextUnderline(vm.TextUnderline);
            }
        }

        private void OnToolbarTextBoldChanged(object? sender, bool isBold)
        {
            ApplySelectedTextBold(isBold);
        }

        private void OnToolbarTextItalicChanged(object? sender, bool isItalic)
        {
            ApplySelectedTextItalic(isItalic);
        }

        private void OnToolbarTextUnderlineChanged(object? sender, bool isUnderline)
        {
            ApplySelectedTextUnderline(isUnderline);
        }

        private void OnToolbarShadowChanged(object? sender, bool isEnabled)
        {
            ApplySelectedShadowState(isEnabled);
        }

        private void OnWidthChanged(object? sender, int width)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SetStrokeWidthCommand.Execute(width);
            }
        }

        private void OnCornerRadiusChanged(object? sender, int cornerRadius)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.CornerRadius = cornerRadius;
            }
        }

        private void OnZoomChanged(object? sender, double zoom)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Zoom = zoom;
            }
        }

        private void OnZoomPickerZoomToFitRequested(object? sender, EventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.ZoomToFitCommand.Execute(null);
            }
        }

        private void OnSidebarScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            // TODO: Restore sidebar scrollbar overlay logic
        }

        private void OnToolbarScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            // TODO: Restore toolbar scrollbar overlay logic
        }

        // --- Restored from ref\3babd33_EditorView.axaml.cs lines 767-829 ---

        private void ApplySelectedColor(string colorHex)
        {
            var selected = _selectionController.SelectedShape;
            if (selected == null) return;

            // Ensure the annotation model property is updated so changes persist and effects render correctly
            if (selected.Tag is Annotation annotation)
            {
                annotation.StrokeColor = colorHex;
            }

            var brush = new SolidColorBrush(Color.Parse(colorHex));

            switch (selected)
            {
                case Shape shape:
                    shape.Stroke = brush;

                    if (shape is global::Avalonia.Controls.Shapes.Path path)
                    {
                        path.Fill = brush;
                    }
                    else if (selected is SpeechBalloonControl balloon)
                    {
                        balloon.InvalidateVisual();
                    }
                    break;
                case OutlinedTextControl textBox:
                    textBox.InvalidateVisual();
                    break;
                case SpeechBalloonControl balloonControl:
                    balloonControl.InvalidateVisual();
                    break;
                case StepControl stepControl:
                    stepControl.InvalidateVisual();
                    break;
            }

            // ISSUE-LIVE-UPDATE: Update active text editor if present
            _selectionController.UpdateActiveTextEditorProperties();
        }

        private void ApplySelectedFillColor(string colorHex)
        {
            var selected = _selectionController.SelectedShape;
            if (selected == null) return;

            // Ensure the annotation model property is updated so changes persist and effects render correctly
            if (selected.Tag is Annotation annotation)
            {
                annotation.FillColor = colorHex;
            }

            var brush = new SolidColorBrush(Color.Parse(colorHex));

            switch (selected)
            {
                case Shape shape:
                    if (shape.Tag is HighlightAnnotation)
                    {
                        OnRequestUpdateEffect(shape);
                        break;
                    }

                    if (shape is global::Avalonia.Controls.Shapes.Path path)
                    {
                        path.Fill = brush;
                    }
                    else if (shape is global::Avalonia.Controls.Shapes.Rectangle || shape is global::Avalonia.Controls.Shapes.Ellipse)
                    {
                        shape.Fill = brush;
                    }
                    break;
                case OutlinedTextControl textBox:
                    textBox.InvalidateVisual();
                    break;
                case SpeechBalloonControl balloonControl:
                    balloonControl.InvalidateVisual();
                    break;
                case StepControl stepControl:
                    stepControl.InvalidateVisual();
                    break;
            }

            // ISSUE-LIVE-UPDATE: Update active text editor if present
            _selectionController.UpdateActiveTextEditorProperties();
        }

        private void ApplySelectedTextColor(string colorHex)
        {
            var selected = _selectionController.SelectedShape;
            if (selected == null) return;

            if (selected.Tag is TextAnnotation textAnnotation)
            {
                textAnnotation.TextColor = colorHex;
            }
            else if (selected.Tag is SpeechBalloonAnnotation balloon)
            {
                balloon.TextColor = colorHex;
            }
            else if (selected.Tag is NumberAnnotation number)
            {
                number.TextColor = colorHex;
            }

            // Update UI
            if (selected is OutlinedTextControl outText)
            {
                outText.InvalidateVisual();
            }
            else if (selected is SpeechBalloonControl balloonControl)
            {
                balloonControl.InvalidateVisual();
            }
            else if (selected is StepControl stepControl)
            {
                stepControl.InvalidateVisual();
            }

            // ISSUE-LIVE-UPDATE: Update active text editor if present
            _selectionController.UpdateActiveTextEditorProperties();
        }

        private void ApplySelectedStrokeWidth(int width)
        {
            var selected = _selectionController.SelectedShape;
            if (selected == null) return;

            if (selected.Tag is Annotation annotation)
            {
                annotation.StrokeWidth = width;
            }

            if (selected.Tag is ArrowAnnotation arrowAnnotation
                && selected is global::Avalonia.Controls.Shapes.Path arrowPath)
            {
                AnnotationVisualFactory.UpdateVisualControl(arrowPath, arrowAnnotation);
                return;
            }

            switch (selected)
            {
                case Shape shape:
                    shape.StrokeThickness = width;
                    break;
                case OutlinedTextControl textBox:
                    textBox.InvalidateMeasure();
                    textBox.InvalidateVisual();
                    break;
                case SpeechBalloonControl balloon:
                    if (balloon.Annotation != null)
                    {
                        balloon.Annotation.StrokeWidth = width;
                        balloon.InvalidateVisual();
                    }
                    break;
                case StepControl stepControl:
                    if (stepControl.Annotation != null)
                    {
                        stepControl.Annotation.StrokeWidth = width;
                        stepControl.InvalidateVisual();
                    }
                    break;
            }
        }

        private void ApplySelectedCornerRadius(int cornerRadius)
        {
            var selected = _selectionController.SelectedShape;
            if (selected == null) return;

            int clampedRadius = Math.Max(0, cornerRadius);

            switch (selected.Tag)
            {
                case RectangleAnnotation rectangleAnnotation:
                    rectangleAnnotation.CornerRadius = clampedRadius;
                    if (selected is global::Avalonia.Controls.Shapes.Rectangle rectangle)
                    {
                        rectangle.RadiusX = clampedRadius;
                        rectangle.RadiusY = clampedRadius;
                    }
                    break;
                case SpeechBalloonAnnotation balloonAnnotation:
                    balloonAnnotation.CornerRadius = clampedRadius;
                    if (selected is SpeechBalloonControl balloonControl)
                    {
                        balloonControl.InvalidateVisual();
                    }
                    break;
            }
        }

        private void ApplySelectedFontSize(float fontSize)
        {
            var selected = _selectionController.SelectedShape;
            if (selected?.Tag is TextAnnotation textAnn)
            {
                textAnn.FontSize = fontSize;
                if (selected is OutlinedTextControl outlinedText)
                {
                    outlinedText.InvalidateMeasure();
                    outlinedText.InvalidateVisual();
                }
            }
            else if (selected?.Tag is NumberAnnotation numAnn)
            {
                numAnn.FontSize = fontSize;

                if (selected is StepControl stepControl)
                {
                    AnnotationVisualFactory.UpdateVisualControl(stepControl, numAnn);
                }
            }
            else if (selected?.Tag is SpeechBalloonAnnotation balloonAnn)
            {
                balloonAnn.FontSize = fontSize;
                if (selected is SpeechBalloonControl balloonControl)
                {
                    balloonControl.InvalidateVisual();
                }
                _selectionController.UpdateActiveTextEditorProperties();
            }
        }

        private void ApplySelectedFontFamily(string fontFamily)
        {
            if (string.IsNullOrWhiteSpace(fontFamily))
            {
                return;
            }

            var selected = _selectionController.SelectedShape;

            if (selected?.Tag is TextAnnotation textAnnotation)
            {
                textAnnotation.FontFamily = fontFamily;

                if (selected is OutlinedTextControl outlinedText)
                {
                    outlinedText.InvalidateMeasure();
                    outlinedText.InvalidateVisual();
                }
            }
            else if (selected?.Tag is SpeechBalloonAnnotation balloonAnnotation)
            {
                balloonAnnotation.FontFamily = fontFamily;

                if (selected is SpeechBalloonControl balloonControl)
                {
                    balloonControl.InvalidateVisual();
                }

                _selectionController.UpdateActiveTextEditorProperties();
            }
        }

        private void ApplySelectedArrowStyle(ArrowStyle arrowStyle)
        {
            var selected = _selectionController.SelectedShape;

            if (selected?.Tag is ArrowAnnotation arrowAnnotation && selected is global::Avalonia.Controls.Shapes.Path arrowPath)
            {
                arrowAnnotation.Style = arrowStyle;
                AnnotationVisualFactory.UpdateVisualControl(arrowPath, arrowAnnotation);
                _selectionController.UpdateSelectionHandles();
            }
        }

        private void ApplySelectedCursorType(CursorType cursorType)
        {
            var selected = _selectionController.SelectedShape;

            if (selected?.Tag is CursorAnnotation cursorAnnotation && selected is Image cursorImage)
            {
                cursorAnnotation.CursorType = cursorType;

                var cursorBitmap = WindowsCursorBitmapRenderer.CreateAnnotationBitmap(cursorType);
                if (cursorBitmap != null)
                {
                    cursorAnnotation.SetImage(cursorBitmap);
                }

                AnnotationVisualFactory.UpdateVisualControl(cursorImage, cursorAnnotation);
                _selectionController.UpdateSelectionHandles();
            }
        }

        private void ApplySelectedEffectStrength(float strength)
        {
            var selected = _selectionController.SelectedShape;
            if (selected?.Tag is BaseEffectAnnotation effectAnn)
            {
                effectAnn.Amount = strength;
                OnRequestUpdateEffect(selected);
            }
            else if (selected?.Tag is SpotlightAnnotation spotlightAnn)
            {
                spotlightAnn.DarkenOpacity = (byte)Math.Clamp(
                    strength / MainViewModel.GetMaxEffectStrength(EditorTool.Spotlight) * 255,
                    0,
                    255);

                if (selected is SpotlightControl spotlightControl)
                {
                    RefreshSpotlightOverlay();
                }
            }
        }

        private void ApplySelectedShadowState(bool isEnabled)
        {
            var selected = _selectionController.SelectedShape;
            if (selected?.Tag is not Annotation annotation)
            {
                return;
            }

            annotation.ShadowEnabled = isEnabled;

            if (selected is Control control)
            {
                if (isEnabled)
                {
                    control.Effect = new Avalonia.Media.DropShadowEffect
                    {
                        OffsetX = 3,
                        OffsetY = 3,
                        BlurRadius = 4,
                        Color = Avalonia.Media.Color.FromArgb(128, 0, 0, 0)
                    };
                }
                else
                {
                    control.Effect = null;
                }
            }
        }

        private void ApplySelectedTextBold(bool isBold)
        {
            if (_selectionController.SelectedShape?.Tag is TextAnnotation textAnn)
            {
                textAnn.IsBold = isBold;
                if (_selectionController.SelectedShape is OutlinedTextControl outlinedText)
                {
                    outlinedText.InvalidateMeasure();
                    outlinedText.InvalidateVisual();
                }
            }
        }

        private void ApplySelectedTextItalic(bool isItalic)
        {
            if (_selectionController.SelectedShape?.Tag is TextAnnotation textAnn)
            {
                textAnn.IsItalic = isItalic;
                if (_selectionController.SelectedShape is OutlinedTextControl outlinedText)
                {
                    outlinedText.InvalidateMeasure();
                    outlinedText.InvalidateVisual();
                }
            }
        }

        private void ApplySelectedTextUnderline(bool isUnderline)
        {
            if (_selectionController.SelectedShape?.Tag is TextAnnotation textAnn)
            {
                textAnn.IsUnderline = isUnderline;
                if (_selectionController.SelectedShape is OutlinedTextControl outlinedText)
                {
                    outlinedText.InvalidateMeasure();
                    outlinedText.InvalidateVisual();
                }
            }
        }

        private static Color ApplyHighlightAlpha(Color baseColor)
        {
            return Color.FromArgb(0x55, baseColor.R, baseColor.G, baseColor.B);
        }

    }
}