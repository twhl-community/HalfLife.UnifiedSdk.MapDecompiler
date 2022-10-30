using System.Collections.Generic;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.ViewModels
{
    public sealed record FileFilter(string Name, List<string> Extensions)
    {
        public FileFilter(string name, string extension)
            : this(name, new List<string> { extension })
        {
        }
    }
}
