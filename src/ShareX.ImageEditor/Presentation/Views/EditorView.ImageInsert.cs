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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ShareX.ImageEditor.Core.Annotations;
using ShareX.ImageEditor.Presentation.ViewModels;
using SkiaSharp;
using System.ComponentModel;

namespace ShareX.ImageEditor.Presentation.Views
{
    public partial class EditorView : UserControl
    {
        private async Task InsertExternalImageAsync(SKBitmap skBitmap, string? sourceFilePath = null)
        {
            if (DataContext is not MainViewModel vm)
            {
                InsertImageAnnotation(skBitmap);
                return;
            }

            if (!vm.HasPreviewImage || _editorCore.SourceImage == null)
            {
                LoadBitmapIntoEditor(vm, skBitmap, sourceFilePath);
                return;
            }

            InsertImagePlacement? placement = await ShowInsertImageDialogAsync(vm, skBitmap);
            if (!placement.HasValue)
            {
                skBitmap.Dispose();
                return;
            }

            await InsertImageWithPlacementAsync(skBitmap, placement.Value);
        }

        private Task<InsertImagePlacement?> ShowInsertImageDialogAsync(MainViewModel vm, SKBitmap skBitmap)
        {
            if (vm.IsModalOpen)
            {
                return Task.FromResult<InsertImagePlacement?>(null);
            }

            var completionSource = new TaskCompletionSource<InsertImagePlacement?>(TaskCreationOptions.RunContinuationsAsynchronously);
            PropertyChangedEventHandler? propertyChangedHandler = null;

            void Complete(InsertImagePlacement? result)
            {
                if (!completionSource.TrySetResult(result))
                {
                    return;
                }

                if (propertyChangedHandler != null)
                {
                    vm.PropertyChanged -= propertyChangedHandler;
                }
            }

            propertyChangedHandler = (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.IsModalOpen) && !vm.IsModalOpen)
                {
                    Complete(null);
                }
            };

            vm.PropertyChanged += propertyChangedHandler;

            var dialog = new InsertImageDialogViewModel(
                skBitmap.Width,
                skBitmap.Height,
                onSelect: placement =>
                {
                    Complete(placement);
                    vm.CloseModalCommand.Execute(null);
                },
                onCancel: () =>
                {
                    Complete(null);
                    vm.CloseModalCommand.Execute(null);
                });

            vm.ModalContent = dialog;
            vm.IsModalOpen = true;

            return completionSource.Task;
        }

        private async Task InsertImageWithPlacementAsync(SKBitmap skBitmap, InsertImagePlacement placement)
        {
            int canvasWidth = (int)Math.Round(_editorCore.CanvasSize.Width);
            int canvasHeight = (int)Math.Round(_editorCore.CanvasSize.Height);
            Point? position = null;
            bool waitForResizeSync = false;

            switch (placement)
            {
                case InsertImagePlacement.Center:
                    position = null;
                    break;
                case InsertImagePlacement.CanvasExpandDown:
                    int rightPadding = Math.Max(0, skBitmap.Width - canvasWidth);
                    _editorCore.ResizeCanvas(top: 0, right: rightPadding, bottom: skBitmap.Height, left: 0, backgroundColor: SKColors.Transparent);
                    position = new Point(0, canvasHeight);
                    waitForResizeSync = true;
                    break;
                case InsertImagePlacement.CanvasExpandRight:
                    int bottomPadding = Math.Max(0, skBitmap.Height - canvasHeight);
                    _editorCore.ResizeCanvas(top: 0, right: skBitmap.Width, bottom: bottomPadding, left: 0, backgroundColor: SKColors.Transparent);
                    position = new Point(canvasWidth, 0);
                    waitForResizeSync = true;
                    break;
            }

            if (waitForResizeSync)
            {
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            }

            InsertImageAnnotationCore(skBitmap, position);
        }

        private void InsertImageAnnotationCore(SKBitmap skBitmap, Point? position = null)
        {
            var canvas = this.FindControl<Canvas>("AnnotationCanvas");
            if (canvas == null || DataContext is not MainViewModel vm)
            {
                return;
            }

            double posX = position?.X ?? (_editorCore.CanvasSize.Width / 2 - skBitmap.Width / 2.0);
            double posY = position?.Y ?? (_editorCore.CanvasSize.Height / 2 - skBitmap.Height / 2.0);

            var annotation = new ImageAnnotation();
            annotation.SetImage(skBitmap);
            annotation.StartPoint = new SKPoint((float)posX, (float)posY);
            annotation.EndPoint = new SKPoint((float)(posX + skBitmap.Width), (float)(posY + skBitmap.Height));

            Control? control = CreateControlForAnnotation(annotation);
            if (control == null)
            {
                return;
            }

            canvas.Children.Add(control);
            _editorCore.AddAnnotation(annotation);
            vm.HasAnnotations = true;
            vm.ActiveTool = EditorTool.Select;
            _selectionController.SetSelectedShape(control);
        }
    }
}