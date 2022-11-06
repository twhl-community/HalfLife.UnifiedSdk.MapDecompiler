using Avalonia.Controls;
using Avalonia.Interactivity;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.Views
{
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
        }

        public void Button_Ok(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
