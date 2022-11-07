using ReactiveUI;
using System.Reactive.Linq;
using System.Windows.Input;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.ViewModels
{
    public sealed class DecompilerOptionsViewModel : ViewModelBase
    {
        private string _outputDirectory = string.Empty;

        public string OutputDirectory
        {
            get => _outputDirectory;
            set => this.RaiseAndSetIfChanged(ref _outputDirectory, value);
        }

        public ICommand BrowseOutputDirectoryCommand { get; }

        public Interaction<OpenDirectoryViewModel, string?> ShowBrowseDirectoryDialog { get; } = new();

        public int SelectedStrategy { get; set; }

        public bool MergeBrushes { get; set; } = true;

        public bool IncludeLiquids { get; set; } = true;

        public BrushOptimization BrushOptimization { get; set; } = BrushOptimization.BestTextureMatch;

        public DecompilerOptionsViewModel()
        {
            BrowseOutputDirectoryCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var directory = await ShowBrowseDirectoryDialog.Handle(new());

                if (directory is null)
                {
                    return;
                }

                OutputDirectory = directory;
            });
        }

        public DecompilerOptions ToOptions()
        {
            return new(
                MergeBrushes: MergeBrushes,
                IncludeLiquids: IncludeLiquids,
                BrushOptimization: BrushOptimization
                );
        }
    }
}
