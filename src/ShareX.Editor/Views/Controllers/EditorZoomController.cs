using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using ShareX.Editor.ViewModels;
using System;

namespace ShareX.Editor.Views.Controllers;

public class EditorZoomController
{
    private readonly EditorView _view;
    private bool _isPointerZooming;
    private double _lastZoom = 1.0;
    private bool _isPanning;
    private Point _panStart;
    private Vector _panOrigin;

    private const double MinZoom = 0.25;
    private const double MaxZoom = 4.0;
    private const double ZoomStep = 0.1;

    public EditorZoomController(EditorView view)
    {
        _view = view;
    }

    public void InitLastZoom(double zoom)
    {
        _lastZoom = zoom;
    }

    public void OnPreviewPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_view.DataContext is not MainViewModel vm) return;
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;

        var oldZoom = vm.Zoom;
        var direction = e.Delta.Y > 0 ? 1 : -1;
        var newZoom = Math.Clamp(Math.Round((oldZoom + direction * ZoomStep) * 100) / 100, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - oldZoom) < 0.0001) return;

        var scrollViewer = _view.FindControl<ScrollViewer>("CanvasScrollViewer");
        if (scrollViewer != null)
        {
            var pointerPosition = e.GetPosition(scrollViewer);
            var offsetBefore = scrollViewer.Offset;
            if (scrollViewer.Extent.Width <= scrollViewer.Viewport.Width)
                offsetBefore = offsetBefore.WithX(0);
            if (scrollViewer.Extent.Height <= scrollViewer.Viewport.Height)
                offsetBefore = offsetBefore.WithY(0);
            var logicalPoint = new Vector(
               (offsetBefore.X + pointerPosition.X) / oldZoom,
               (offsetBefore.Y + pointerPosition.Y) / oldZoom);

            _isPointerZooming = true;
            _lastZoom = oldZoom;
            vm.Zoom = newZoom;

            Dispatcher.UIThread.Post(() =>
            {
                var targetOffset = new Vector(
                    logicalPoint.X * newZoom - pointerPosition.X,
                    logicalPoint.Y * newZoom - pointerPosition.Y);

                var maxX = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
                var maxY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);

                if (scrollViewer.Extent.Width <= scrollViewer.Viewport.Width)
                    targetOffset = targetOffset.WithX(0);
                if (scrollViewer.Extent.Height <= scrollViewer.Viewport.Height)
                    targetOffset = targetOffset.WithY(0);

                scrollViewer.Offset = new Vector(
                    Math.Clamp(targetOffset.X, 0, maxX),
                    Math.Clamp(targetOffset.Y, 0, maxY));
            }, DispatcherPriority.Render);
        }
        else
        {
            _lastZoom = oldZoom;
            vm.Zoom = newZoom;
        }

        _isPointerZooming = false;
        _lastZoom = vm.Zoom;
        e.Handled = true;
    }

    public void AdjustZoomToAnchor(double oldZoom, double newZoom, Point anchor)
    {
        var scrollViewer = _view.FindControl<ScrollViewer>("CanvasScrollViewer");
        if (scrollViewer == null || oldZoom <= 0) return;

        var offsetBefore = scrollViewer.Offset;
        if (scrollViewer.Extent.Width <= scrollViewer.Viewport.Width)
            offsetBefore = offsetBefore.WithX(0);
        if (scrollViewer.Extent.Height <= scrollViewer.Viewport.Height)
            offsetBefore = offsetBefore.WithY(0);
        var logicalPoint = new Vector(
            (offsetBefore.X + anchor.X) / oldZoom,
            (offsetBefore.Y + anchor.Y) / oldZoom);

        Dispatcher.UIThread.Post(() =>
        {
            var targetOffset = new Vector(
                logicalPoint.X * newZoom - anchor.X,
                logicalPoint.Y * newZoom - anchor.Y);

            var maxX = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
            var maxY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);

            if (scrollViewer.Extent.Width <= scrollViewer.Viewport.Width)
                targetOffset = targetOffset.WithX(0);
            if (scrollViewer.Extent.Height <= scrollViewer.Viewport.Height)
                targetOffset = targetOffset.WithY(0);

            scrollViewer.Offset = new Vector(
                Math.Clamp(targetOffset.X, 0, maxX),
                Math.Clamp(targetOffset.Y, 0, maxY));
        }, DispatcherPriority.Render);
    }

    public void CenterCanvasOnZoomChange()
    {
        var scrollViewer = _view.FindControl<ScrollViewer>("CanvasScrollViewer");
        if (scrollViewer == null) return;

        Dispatcher.UIThread.Post(() =>
        {
            var extent = scrollViewer.Extent;
            var viewport = scrollViewer.Viewport;
            var targetOffset = new Vector(
                Math.Max(0, (extent.Width - viewport.Width) / 2),
                Math.Max(0, (extent.Height - viewport.Height) / 2));

            scrollViewer.Offset = targetOffset;
        }, DispatcherPriority.Render);
    }

    public void OnScrollViewerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer) return;

        var properties = e.GetCurrentPoint(scrollViewer).Properties;
        if (!properties.IsMiddleButtonPressed) return;

        _isPanning = true;
        _panStart = e.GetPosition(scrollViewer);
        _panOrigin = scrollViewer.Offset;
        scrollViewer.Cursor = new Cursor(StandardCursorType.SizeAll);
        e.Pointer.Capture(scrollViewer);
        e.Handled = true;
    }

    public void OnScrollViewerPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning || sender is not ScrollViewer scrollViewer) return;

        var current = e.GetPosition(scrollViewer);
        var delta = current - _panStart;

        var target = new Vector(
            _panOrigin.X - delta.X,
            _panOrigin.Y - delta.Y);

        var maxX = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
        var maxY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);

        scrollViewer.Offset = new Vector(
            Math.Clamp(target.X, 0, maxX),
            Math.Clamp(target.Y, 0, maxY));

        e.Handled = true;
    }

    public void OnScrollViewerPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer) return;

        if (_isPanning)
        {
            _isPanning = false;
            scrollViewer.Cursor = null;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    public void ResetScrollViewerOffset()
    {
        var scrollViewer = _view.FindControl<ScrollViewer>("CanvasScrollViewer");
        if (scrollViewer == null) return;

        Dispatcher.UIThread.Post(() => scrollViewer.Offset = new Vector(0, 0), DispatcherPriority.Render);
    }

    public void HandleZoomPropertyChanged(MainViewModel vm)
    {
         if (!_isPointerZooming)
        {
            var scrollViewer = _view.FindControl<ScrollViewer>("CanvasScrollViewer");
            if (scrollViewer != null)
            {
                var anchor = new Point(scrollViewer.Viewport.Width / 2, scrollViewer.Viewport.Height / 2);
                AdjustZoomToAnchor(_lastZoom, vm.Zoom, anchor);
            }
            _lastZoom = vm.Zoom;
        }
        else
        {
            _lastZoom = vm.Zoom;
        }
    }
}
