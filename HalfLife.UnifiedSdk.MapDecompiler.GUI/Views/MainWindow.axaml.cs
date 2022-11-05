using Avalonia.Controls;
using Avalonia.ReactiveUI;
using HalfLife.UnifiedSdk.MapDecompiler.GUI.ViewModels;
using ReactiveUI;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.Views
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        public MainWindow()
        {
            InitializeComponent();

            this.WhenActivated(d =>
            {
                d(ViewModel!.ShowConvertFilesDialog.RegisterHandler(DoShowOpenFileDialogAsync));
                d(ViewModel!.ShowCancelJobsDialog.RegisterHandler(DoShowCancelJobsDialogAsync));
            });
        }

        // See https://stackoverflow.com/a/49013345/1306648
        public async void Window_Closing(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;

            var shouldClose = await ViewModel!.ShouldClose();

            // Cancel close if we've got ongoing work.
            if (!shouldClose)
            {
                return;
            }

            Closing -= Window_Closing;

            await ViewModel!.OnClosing();

            Close();
        }

        private async Task DoShowOpenFileDialogAsync(InteractionContext<OpenFileViewModel, string[]?> interaction)
        {
            var dialog = new OpenFileDialog
            {
                Title = interaction.Input.Title,
                Filters = interaction.Input.Filters.Select(f => new FileDialogFilter
                {
                    Name = f.Name,
                    Extensions = f.Extensions
                }).ToList(),
                AllowMultiple = interaction.Input.AllowMultiple
            };

            var result = await dialog.ShowAsync(this);
            interaction.SetOutput(result);
        }

        private async Task DoShowCancelJobsDialogAsync(InteractionContext<CancelJobsDialogViewModel, bool> interaction)
        {
            var dialog = new CancelJobsDialog
            {
                DataContext = interaction.Input
            };

            var result = await dialog.ShowDialog<bool>(this);

            interaction.SetOutput(result);
        }
    }
}