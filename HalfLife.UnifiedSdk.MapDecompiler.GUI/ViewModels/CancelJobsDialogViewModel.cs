using ReactiveUI;
using System.Reactive;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.ViewModels
{
    public sealed class CancelJobsDialogViewModel : ViewModelBase
    {
        public ReactiveCommand<Unit, bool> YesCommand { get; }

        public ReactiveCommand<Unit, bool> CancelCommand { get; }

        public CancelJobsDialogViewModel()
        {
            YesCommand = ReactiveCommand.Create(() => true);
            CancelCommand = ReactiveCommand.Create(() => false);
        }
    }
}
