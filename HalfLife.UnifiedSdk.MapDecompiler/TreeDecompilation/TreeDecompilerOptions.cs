namespace HalfLife.UnifiedSdk.MapDecompiler.TreeDecompilation
{
    public sealed record TreeDecompilerOptions(
        bool MergeBrushes = false,
        bool IncludeLiquids = false,
        BrushOptimization BrushOptimization = BrushOptimization.BestTextureMatch);
}
