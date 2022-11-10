using System.Collections.Generic;
using System.Linq;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.ViewModels
{
    public sealed class OpenFileViewModel : ViewModelBase
    {
        public string? Title { get; set; }

        public IEnumerable<FileFilter> Filters { get; set; } = Enumerable.Empty<FileFilter>();

        public bool AllowMultiple { get; set; } = false;

        public string? Directory { get; set; }
    }
}
