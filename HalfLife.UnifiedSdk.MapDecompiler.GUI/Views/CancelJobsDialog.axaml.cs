using Avalonia.ReactiveUI;
using HalfLife.UnifiedSdk.MapDecompiler.GUI.ViewModels;
using ReactiveUI;
using System;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.Views
{
    public partial class CancelJobsDialog : ReactiveWindow<CancelJobsDialogViewModel>
    {
        public CancelJobsDialog()
        {
            InitializeComponent();

            this.WhenActivated(d =>
            {
                d(ViewModel!.YesCommand.Subscribe(b => Close(b)));
                d(ViewModel!.CancelCommand.Subscribe(b => Close(b)));
            });
        }
    }
}
