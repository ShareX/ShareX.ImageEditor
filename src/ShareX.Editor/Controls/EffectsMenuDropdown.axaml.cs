using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ShareX.Editor.Controls
{
    public partial class EffectsMenuDropdown : UserControl
    {
        // Events
        public event EventHandler? BrightnessRequested;
        public event EventHandler? ContrastRequested;
        public event EventHandler? HueRequested;
        public event EventHandler? SaturationRequested;
        public event EventHandler? GammaRequested;
        public event EventHandler? AlphaRequested;
        
        public event EventHandler? InvertRequested;
        public event EventHandler? BlackAndWhiteRequested;
        public event EventHandler? SepiaRequested;
        public event EventHandler? PolaroidRequested;
        public event EventHandler? ColorizeRequested;
        public event EventHandler? SelectiveColorRequested;

        public EffectsMenuDropdown()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnDropdownButtonClick(object? sender, RoutedEventArgs e)
        {
            var popup = this.FindControl<Popup>("EffectsPopup");
            if (popup != null)
            {
                popup.IsOpen = !popup.IsOpen;
            }
        }

        private void ClosePopup()
        {
            var popup = this.FindControl<Popup>("EffectsPopup");
            if (popup != null)
            {
                popup.IsOpen = false;
            }
        }

        private void OnBrightnessClick(object? sender, RoutedEventArgs e)
        {
            ClosePopup();
            BrightnessRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnContrastClick(object? sender, RoutedEventArgs e)
        {
            ClosePopup();
            ContrastRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnHueClick(object? sender, RoutedEventArgs e)
        {
            ClosePopup();
            HueRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnSaturationClick(object? sender, RoutedEventArgs e)
        {
            ClosePopup();
            SaturationRequested?.Invoke(this, EventArgs.Empty);
        }
        
        private void OnGammaClick(object? sender, RoutedEventArgs e)
        {
            ClosePopup();
            GammaRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnAlphaClick(object? sender, RoutedEventArgs e)
        {
            ClosePopup();
            AlphaRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnInvertClick(object? sender, RoutedEventArgs e)
        {
            ClosePopup();
            InvertRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnBlackAndWhiteClick(object? sender, RoutedEventArgs e)
        {
            ClosePopup();
            BlackAndWhiteRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnSepiaClick(object? sender, RoutedEventArgs e)
        {
            ClosePopup();
            SepiaRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnPolaroidClick(object? sender, RoutedEventArgs e)
        {
            ClosePopup();
            PolaroidRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnColorizeClick(object? sender, RoutedEventArgs e)
        {
            ClosePopup();
            ColorizeRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnSelectiveColorClick(object? sender, RoutedEventArgs e)
        {
            ClosePopup();
            SelectiveColorRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
