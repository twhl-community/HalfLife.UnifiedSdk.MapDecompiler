using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using Avalonia.Styling;
using Avalonia.Threading;
using HalfLife.UnifiedSdk.MapDecompiler.GUI.ViewModels;
using ReactiveUI;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.Views
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        private bool _performedInitialLayout;

        public MainWindow()
        {
            InitializeComponent();

            this.WhenActivated(d =>
            {
                if (!_performedInitialLayout)
                {
                    _performedInitialLayout = true;

                    // This is necessary so the decompiler options view is sized according to the first tab,
                    // which is the largest of the two.
                    // On startup we switch to the first tab and then delay switching to the remembered tab
                    // until after the initial layout has been performed.
                    var selectedStrategy = Settings.Default.DecompilerStrategy;

                    Settings.Default.DecompilerStrategy = DecompilerStrategies.Strategies[0].Name;

                    Dispatcher.UIThread.Post(() => Settings.Default.DecompilerStrategy = selectedStrategy);
                }

                d(ViewModel!.ShowConvertFilesDialog.RegisterHandler(DoShowOpenFileDialogAsync));
                d(ViewModel!.QuitApplication.RegisterHandler(DoQuitApplication));
                d(ViewModel!.ShowCancelAllJobsDialog.RegisterHandler(DoShowCancelAllJobsDialogAsync));
                d(ViewModel!.DecompilerOptions.ShowBrowseDirectoryDialog.RegisterHandler(DoShowOpenDirectoryDialogAsync));
            });
        }

        public void Theme_ChangeToLight(object? sender, RoutedEventArgs e)
        {
            Settings.Default.Theme = ThemeVariant.Light;
        }

        public void Theme_ChangeToDark(object? sender, RoutedEventArgs e)
        {
            Settings.Default.Theme = ThemeVariant.Dark;
        }

        public async void About_Click(object? sender, RoutedEventArgs e)
        {
            var aboutDialog = new AboutDialog();

            await aboutDialog.ShowDialog(this);
        }

        // See https://stackoverflow.com/a/49013345/1306648
        public async void Window_Closing(object? sender, WindowClosingEventArgs e)
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

            await Settings.Default.Save();

            Close();
        }

        private async Task DoShowOpenFileDialogAsync(InteractionContext<OpenFileViewModel, IReadOnlyList<string>?> interaction)
        {
            var options = new FilePickerOpenOptions
            {
                Title = interaction.Input.Title,
                FileTypeFilter = interaction.Input.Filters.Select(f => new FilePickerFileType(f.Name)
                {
                    Patterns = f.Extensions
                }).ToList(),
                AllowMultiple = interaction.Input.AllowMultiple
            };

            var result = await StorageProvider.OpenFilePickerAsync(options);
            interaction.SetOutput(result.Select(f => f.Path.LocalPath).ToList());
        }

        private void DoQuitApplication(InteractionContext<Unit, Unit> interaction)
        {
            Close();
            interaction.SetOutput(new());
        }

        private async Task DoShowOpenDirectoryDialogAsync(InteractionContext<OpenDirectoryViewModel, string?> interaction)
        {
            var options = new FolderPickerOpenOptions
            {
                Title = interaction.Input.Title,
                AllowMultiple = false
            };

            if (interaction.Input.Directory is not null)
            {
                options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(new Uri(interaction.Input.Directory));
            }

            var result = await StorageProvider.OpenFolderPickerAsync(options);

            if (result.Count > 0)
            {
                interaction.SetOutput(result[0].Path.LocalPath);
            }
            else
            {
                interaction.SetOutput(null);
            }
        }

        private async Task DoShowCancelAllJobsDialogAsync(InteractionContext<CancelAllJobsDialogViewModel, bool> interaction)
        {
            var dialog = new CancelAllJobsDialog
            {
                DataContext = interaction.Input
            };

            var result = await dialog.ShowDialog<bool>(this);

            interaction.SetOutput(result);
        }
    }
}
