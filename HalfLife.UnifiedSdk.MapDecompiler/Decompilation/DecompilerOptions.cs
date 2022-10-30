namespace HalfLife.UnifiedSdk.MapDecompiler.Decompilation
{
    public sealed record DecompilerOptions(
        bool MergeBrushes = false,
        bool IncludeLiquids = false,
        BrushOptimization BrushOptimization = BrushOptimization.BestTextureMatch);
}
