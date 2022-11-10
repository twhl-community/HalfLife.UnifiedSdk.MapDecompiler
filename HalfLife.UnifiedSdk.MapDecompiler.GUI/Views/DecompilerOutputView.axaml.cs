using Avalonia.Controls;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.Views
{
    public partial class DecompilerOutputView : UserControl
    {
        public DecompilerOutputView()
        {
            InitializeComponent();
            ProgramTextArea.Options.AllowScrollBelowDocument = false;
            JobTextArea.Options.AllowScrollBelowDocument = false;
        }
    }
}
