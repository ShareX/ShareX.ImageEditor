using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ShareX.ImageEditor.Presentation.Views
{
    public partial class ConfirmationDialogView : UserControl
    {
        public ConfirmationDialogView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            this.FindControl<Button>("YesButton")?.Focus();
        }
    }
}
