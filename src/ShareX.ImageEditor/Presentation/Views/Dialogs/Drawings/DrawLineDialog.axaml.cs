using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ShareX.ImageEditor.Core.ImageEffects.Drawings;
using ShareX.ImageEditor.Presentation.Controls;
using SkiaSharp;

namespace ShareX.ImageEditor.Presentation.Views.Dialogs
{
    public partial class DrawLineDialog : UserControl, IEffectDialog
    {
        private readonly DrawLineEffect _defaultEffect = new();

        public event EventHandler<EffectEventArgs>? ApplyRequested;
        public event EventHandler<EffectEventArgs>? PreviewRequested;
        public event EventHandler? CancelRequested;

        public DrawLineDialog()
        {
            AvaloniaXamlLoader.Load(this);
            ApplyDefaults();
            SubscribeColorPicker("LineColorPicker");
            AttachedToVisualTree += (s, e) => RequestPreview();
        }

        private void ApplyDefaults()
        {
            if (this.FindControl<NumericUpDown>("StartXInput") is NumericUpDown startXInput)
            {
                startXInput.Value = _defaultEffect.StartPoint.X;
            }

            if (this.FindControl<NumericUpDown>("StartYInput") is NumericUpDown startYInput)
            {
                startYInput.Value = _defaultEffect.StartPoint.Y;
            }

            if (this.FindControl<NumericUpDown>("EndXInput") is NumericUpDown endXInput)
            {
                endXInput.Value = _defaultEffect.EndPoint.X;
            }

            if (this.FindControl<NumericUpDown>("EndYInput") is NumericUpDown endYInput)
            {
                endYInput.Value = _defaultEffect.EndPoint.Y;
            }

            if (this.FindControl<NumericUpDown>("ThicknessInput") is NumericUpDown thicknessInput)
            {
                thicknessInput.Value = (decimal)_defaultEffect.Thickness;
            }

            if (this.FindControl<ColorPickerDropdown>("LineColorPicker") is ColorPickerDropdown colorPicker)
            {
                colorPicker.SelectedColorValue = ToAvaloniaColor(_defaultEffect.Color);
            }
        }

        private void SubscribeColorPicker(string name)
        {
            ColorPickerDropdown? picker = this.FindControl<ColorPickerDropdown>(name);
            if (picker != null)
            {
                picker.PropertyChanged += OnColorPickerPropertyChanged;
            }
        }

        private void OnColorPickerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ColorPickerDropdown.SelectedColorValueProperty && IsLoaded)
            {
                RequestPreview();
            }
        }

        private int GetInt(string name, int fallback)
        {
            NumericUpDown? control = this.FindControl<NumericUpDown>(name);
            return (int)Math.Round(control?.Value ?? fallback);
        }

        private float GetFloat(string name, float fallback)
        {
            NumericUpDown? control = this.FindControl<NumericUpDown>(name);
            return control?.Value is decimal value ? (float)value : fallback;
        }

        private static SKColor ToSkColor(Avalonia.Media.Color color)
        {
            return new SKColor(color.R, color.G, color.B, color.A);
        }

        private static Avalonia.Media.Color ToAvaloniaColor(SKColor color)
        {
            return Avalonia.Media.Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
        }

        private DrawLineEffect CreateEffect()
        {
            ColorPickerDropdown? colorPicker = this.FindControl<ColorPickerDropdown>("LineColorPicker");

            return new DrawLineEffect
            {
                StartPoint = new SKPointI(GetInt("StartXInput", _defaultEffect.StartPoint.X), GetInt("StartYInput", _defaultEffect.StartPoint.Y)),
                EndPoint = new SKPointI(GetInt("EndXInput", _defaultEffect.EndPoint.X), GetInt("EndYInput", _defaultEffect.EndPoint.Y)),
                Thickness = GetFloat("ThicknessInput", _defaultEffect.Thickness),
                Color = ToSkColor(colorPicker?.SelectedColorValue ?? ToAvaloniaColor(_defaultEffect.Color))
            };
        }

        private void RequestPreview()
        {
            PreviewRequested?.Invoke(this, new EffectEventArgs(
                img => CreateEffect().Apply(img),
                "Line"));
        }

        private void OnNumericChanged(object? sender, NumericUpDownValueChangedEventArgs e)
        {
            if (IsLoaded)
            {
                RequestPreview();
            }
        }

        private void OnApplyClick(object? sender, RoutedEventArgs e)
        {
            ApplyRequested?.Invoke(this, new EffectEventArgs(
                img => CreateEffect().Apply(img),
                "Applied line"));
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
