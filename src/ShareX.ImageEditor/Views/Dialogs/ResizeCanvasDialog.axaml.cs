using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using SkiaSharp;

namespace ShareX.ImageEditor.Views.Dialogs
{
    public partial class ResizeCanvasDialog : UserControl
    {
        public static readonly StyledProperty<int> TopPaddingProperty =
            AvaloniaProperty.Register<ResizeCanvasDialog, int>(nameof(TopPadding), 0);

        public static readonly StyledProperty<int> RightPaddingProperty =
            AvaloniaProperty.Register<ResizeCanvasDialog, int>(nameof(RightPadding), 0);

        public static readonly StyledProperty<int> BottomPaddingProperty =
            AvaloniaProperty.Register<ResizeCanvasDialog, int>(nameof(BottomPadding), 0);

        public static readonly StyledProperty<int> LeftPaddingProperty =
            AvaloniaProperty.Register<ResizeCanvasDialog, int>(nameof(LeftPadding), 0);

        public static readonly StyledProperty<IBrush> CanvasColorBrushProperty =
            AvaloniaProperty.Register<ResizeCanvasDialog, IBrush>(nameof(CanvasColorBrush), Brushes.Transparent);

        public int TopPadding
        {
            get => GetValue(TopPaddingProperty);
            set => SetValue(TopPaddingProperty, value);
        }

        public int RightPadding
        {
            get => GetValue(RightPaddingProperty);
            set => SetValue(RightPaddingProperty, value);
        }

        public int BottomPadding
        {
            get => GetValue(BottomPaddingProperty);
            set => SetValue(BottomPaddingProperty, value);
        }

        public int LeftPadding
        {
            get => GetValue(LeftPaddingProperty);
            set => SetValue(LeftPaddingProperty, value);
        }

        public IBrush CanvasColorBrush
        {
            get => GetValue(CanvasColorBrushProperty);
            set => SetValue(CanvasColorBrushProperty, value);
        }

        private SKColor _canvasColor = SKColors.Transparent;
        private SKColor? _edgeColor;

        public event EventHandler<ResizeCanvasEventArgs>? ApplyRequested;
        public event EventHandler? CancelRequested;

        public ResizeCanvasDialog()
        {
            AvaloniaXamlLoader.Load(this);
            UpdateColorBrush();
        }

        public void Initialize(SKColor? edgeColor = null)
        {
            _edgeColor = edgeColor;
        }

        private void OnColorPresetChanged(object? sender, SelectionChangedEventArgs e)
        {
            var combo = sender as ComboBox;
            if (combo == null) return;

            _canvasColor = combo.SelectedIndex switch
            {
                0 => SKColors.Transparent,
                1 => SKColors.White,
                2 => SKColors.Black,
                3 => _edgeColor ?? SKColors.Transparent,
                _ => SKColors.Transparent
            };

            UpdateColorBrush();
        }

        private void UpdateColorBrush()
        {
            if (_canvasColor.Alpha == 0)
            {
                // Checkerboard pattern for transparent
                CanvasColorBrush = new SolidColorBrush(Color.FromArgb(50, 128, 128, 128));
            }
            else
            {
                CanvasColorBrush = new SolidColorBrush(
                    Color.FromArgb(_canvasColor.Alpha, _canvasColor.Red, _canvasColor.Green, _canvasColor.Blue));
            }
        }

        private void OnApplyClick(object? sender, RoutedEventArgs e)
        {
            ApplyRequested?.Invoke(this, new ResizeCanvasEventArgs(
                TopPadding, RightPadding, BottomPadding, LeftPadding, _canvasColor));
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public class ResizeCanvasEventArgs : EventArgs
    {
        public int Top { get; }
        public int Right { get; }
        public int Bottom { get; }
        public int Left { get; }
        public SKColor BackgroundColor { get; }

        public ResizeCanvasEventArgs(int top, int right, int bottom, int left, SKColor backgroundColor)
        {
            Top = top;
            Right = right;
            Bottom = bottom;
            Left = left;
            BackgroundColor = backgroundColor;
        }
    }
}
