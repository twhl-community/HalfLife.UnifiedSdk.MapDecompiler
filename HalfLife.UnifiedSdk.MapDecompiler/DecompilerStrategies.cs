using HalfLife.UnifiedSdk.MapDecompiler.FaceToBrushDecompilation;
using HalfLife.UnifiedSdk.MapDecompiler.TreeDecompilation;
using System.Collections.Immutable;

namespace HalfLife.UnifiedSdk.MapDecompiler
{
    public static class DecompilerStrategies
    {
        public static DecompilerStrategy TreeDecompilerStrategy { get; } = new TreeDecompilerStrategy();

        public static DecompilerStrategy FaceToBrushDecompilerStrategy { get; } = new FaceToBrushDecompilerStrategy();

        public static ImmutableArray<DecompilerStrategy> Strategies { get; } = ImmutableArray.Create(
            TreeDecompilerStrategy,
            FaceToBrushDecompilerStrategy);
    }
}
