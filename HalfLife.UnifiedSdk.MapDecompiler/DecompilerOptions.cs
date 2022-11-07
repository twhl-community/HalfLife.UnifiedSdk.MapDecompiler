namespace HalfLife.UnifiedSdk.MapDecompiler
{
    /// <summary>
    /// Options for decompiler algorithms. Not all options apply to all decompilers.
    /// </summary>
    /// <param name="MergeBrushes"></param>
    /// <param name="IncludeLiquids"></param>
    /// <param name="BrushOptimization"></param>
    public sealed record DecompilerOptions(
        bool MergeBrushes = false,
        bool IncludeLiquids = false,
        BrushOptimization BrushOptimization = BrushOptimization.BestTextureMatch);
}
