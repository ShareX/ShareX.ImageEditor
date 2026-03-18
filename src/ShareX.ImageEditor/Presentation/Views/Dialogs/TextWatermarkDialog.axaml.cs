using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ShareX.ImageEditor.Core.ImageEffects.Drawings;
using ShareX.ImageEditor.Presentation.Controls;
using SkiaSharp;

namespace ShareX.ImageEditor.Presentation.Views.Dialogs
{
    public partial class TextWatermarkDialog : UserControl, IEffectDialog
    {
        public event EventHandler<EffectEventArgs>? ApplyRequested;
        public event EventHandler<EffectEventArgs>? PreviewRequested;
        public event EventHandler? CancelRequested;

        public TextWatermarkDialog()
        {
            AvaloniaXamlLoader.Load(this);

            SubscribeColorPicker("TextColorPicker");
            SubscribeColorPicker("BackgroundColorPicker");
            SubscribeColorPicker("BorderColorPicker");
            SubscribeColorPicker("ShadowColorPicker");

            AttachedToVisualTree += (s, e) => RequestPreview();
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

        private bool GetBool(string name, bool fallback = false)
        {
            return this.FindControl<CheckBox>(name)?.IsChecked ?? fallback;
        }

        private string GetText(string name, string fallback)
        {
            return this.FindControl<TextBox>(name)?.Text ?? fallback;
        }

        private DrawingPlacement GetPlacement()
        {
            return this.FindControl<ComboBox>("PlacementComboBox")?.SelectedIndex switch
            {
                1 => DrawingPlacement.TopCenter,
                2 => DrawingPlacement.TopRight,
                3 => DrawingPlacement.MiddleLeft,
                4 => DrawingPlacement.MiddleCenter,
                5 => DrawingPlacement.MiddleRight,
                6 => DrawingPlacement.BottomLeft,
                7 => DrawingPlacement.BottomCenter,
                8 => DrawingPlacement.BottomRight,
                _ => DrawingPlacement.TopLeft
            };
        }

        private static SKColor ToSkColor(Avalonia.Media.Color color)
        {
            return new SKColor(color.R, color.G, color.B, color.A);
        }

        private TextWatermarkEffect CreateEffect()
        {
            ColorPickerDropdown? textColorPicker = this.FindControl<ColorPickerDropdown>("TextColorPicker");
            ColorPickerDropdown? backgroundColorPicker = this.FindControl<ColorPickerDropdown>("BackgroundColorPicker");
            ColorPickerDropdown? borderColorPicker = this.FindControl<ColorPickerDropdown>("BorderColorPicker");
            ColorPickerDropdown? shadowColorPicker = this.FindControl<ColorPickerDropdown>("ShadowColorPicker");

            return new TextWatermarkEffect
            {
                Text = GetText("TextContentInput", "Text watermark"),
                Placement = GetPlacement(),
                Offset = new SKPointI(GetInt("OffsetXInput", 5), GetInt("OffsetYInput", 5)),
                AutoHide = GetBool("AutoHideCheckBox"),
                FontFamily = GetText("FontFamilyTextBox", "Arial"),
                FontSize = GetFloat("FontSizeInput", 15f),
                Bold = GetBool("BoldCheckBox"),
                Italic = GetBool("ItalicCheckBox"),
                TextColor = ToSkColor(textColorPicker?.SelectedColorValue ?? Avalonia.Media.Color.FromArgb(255, 235, 235, 235)),
                DrawTextShadow = GetBool("ShadowCheckBox"),
                TextShadowColor = ToSkColor(shadowColorPicker?.SelectedColorValue ?? Avalonia.Media.Colors.Black),
                TextShadowOffset = new SKPointI(GetInt("ShadowOffsetXInput", -1), GetInt("ShadowOffsetYInput", -1)),
                CornerRadius = GetInt("CornerRadiusInput", 4),
                PaddingLeft = GetInt("PaddingLeftInput", 5),
                PaddingTop = GetInt("PaddingTopInput", 5),
                PaddingRight = GetInt("PaddingRightInput", 5),
                PaddingBottom = GetInt("PaddingBottomInput", 5),
                DrawBorder = GetBool("BorderCheckBox", true),
                BorderColor = ToSkColor(borderColorPicker?.SelectedColorValue ?? Avalonia.Media.Colors.Black),
                BorderSize = GetInt("BorderSizeInput", 1),
                DrawBackground = GetBool("BackgroundCheckBox", true),
                BackgroundColor = ToSkColor(backgroundColorPicker?.SelectedColorValue ?? Avalonia.Media.Color.FromArgb(255, 42, 47, 56))
            };
        }

        private void RequestPreview()
        {
            PreviewRequested?.Invoke(this, new EffectEventArgs(
                img => CreateEffect().Apply(img),
                "Text watermark"));
        }

        private void OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (IsLoaded)
            {
                RequestPreview();
            }
        }

        private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                RequestPreview();
            }
        }

        private void OnNumericChanged(object? sender, NumericUpDownValueChangedEventArgs e)
        {
            if (IsLoaded)
            {
                RequestPreview();
            }
        }

        private void OnSettingChanged(object? sender, RoutedEventArgs e)
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
                "Applied text watermark"));
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
