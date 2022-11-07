using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using HalfLife.UnifiedSdk.MapDecompiler.GUI.ViewModels;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.Views
{
    public partial class AboutDialog : ReactiveWindow<AboutDialogViewModel>
    {
        public AboutDialog()
        {
            InitializeComponent();
            ViewModel = new();
        }

        public void Button_Ok(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
