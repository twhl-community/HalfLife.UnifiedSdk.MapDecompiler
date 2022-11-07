using HalfLife.UnifiedSdk.MapDecompiler.Serialization;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.ViewModels
{
    public sealed class AboutDialogViewModel : ViewModelBase
    {
        public string Version { get; } = MapSerialization.Version;
    }
}
