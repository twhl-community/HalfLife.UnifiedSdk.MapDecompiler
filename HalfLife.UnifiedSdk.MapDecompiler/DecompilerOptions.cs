using System.Collections.Immutable;

namespace HalfLife.UnifiedSdk.MapDecompiler
{
    /// <summary>
    /// Options for decompiler algorithms. Not all options apply to all decompilers.
    /// </summary>
    public sealed class DecompilerOptions
    {
        public bool ApplyNullToGeneratedFaces { get; init; }

        public bool AlwaysGenerateOriginBrushes { get; init; }

        public bool MergeBrushes { get; init; }

        public bool IncludeLiquids { get; init; }

        public BrushOptimization BrushOptimization { get; init; } = BrushOptimization.BestTextureMatch;

        public ImmutableList<string> TriggerEntityWildcards { get; init; } = ImmutableList<string>.Empty;
    }
}
