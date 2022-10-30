using Avalonia.Controls;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.Views
{
    public partial class DecompilerOutputView : UserControl
    {
        private bool _programAutoScroll = true;
        private bool _jobAutoScroll = true;

        public DecompilerOutputView()
        {
            InitializeComponent();
        }

        private void ProgramScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            ScrollViewerScrollChanged(ProgramScrollViewer, e, ref _programAutoScroll);
        }

        private void JobScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            ScrollViewerScrollChanged(JobScrollViewer, e, ref _jobAutoScroll);
        }

        // Based on https://stackoverflow.com/a/19315242/1306648
        private static void ScrollViewerScrollChanged(ScrollViewer sv, ScrollChangedEventArgs e, ref bool autoScroll)
        {
            // User scroll event : set or unset auto-scroll mode
            if (e.ExtentDelta.Length == 0)
            {   // Content unchanged : user scroll event
                if (sv.Offset.Y == (sv.Extent.Height - sv.Viewport.Height))
                {   // Scroll bar is in bottom
                    // Set auto-scroll mode
                    autoScroll = true;
                }
                else
                {   // Scroll bar isn't in bottom
                    // Unset auto-scroll mode
                    autoScroll = false;
                }
            }

            // Content scroll event : auto-scroll eventually
            if (autoScroll && e.ExtentDelta.Length != 0)
            {   // Content changed and auto-scroll mode set
                // Autoscroll
                sv.ScrollToEnd();
            }
        }
    }
}
