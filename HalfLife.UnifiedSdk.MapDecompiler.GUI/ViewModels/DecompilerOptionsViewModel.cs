using HalfLife.UnifiedSdk.MapDecompiler.Decompilation;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.ViewModels
{
    public sealed class DecompilerOptionsViewModel : ViewModelBase
    {
        public bool MergeBrushes { get; set; } = true;

        public bool IncludeLiquids { get; set; } = true;

        public BrushOptimization BrushOptimization { get; set; } = BrushOptimization.BestTextureMatch;

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
