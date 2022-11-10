using ReactiveUI;
using System.Reactive.Linq;
using System.Windows.Input;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.ViewModels
{
    public sealed class DecompilerOptionsViewModel : ViewModelBase
    {
        public Settings Settings { get; } = Settings.Default;

        public ICommand BrowseOutputDirectoryCommand { get; }

        public Interaction<OpenDirectoryViewModel, string?> ShowBrowseDirectoryDialog { get; } = new();

        public DecompilerOptionsViewModel()
        {
            BrowseOutputDirectoryCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var directory = await ShowBrowseDirectoryDialog.Handle(new()
                {
                    Directory = Settings.OutputDirectory
                });

                if (directory is null)
                {
                    return;
                }

                Settings.OutputDirectory = directory;
            });
        }

        public DecompilerOptions ToOptions()
        {
            return new(
                MergeBrushes: Settings.MergeBrushes,
                IncludeLiquids: Settings.IncludeLiquids,
                BrushOptimization: Settings.BrushOptimization
                );
        }
    }
}
