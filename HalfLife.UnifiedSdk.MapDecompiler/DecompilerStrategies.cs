using HalfLife.UnifiedSdk.MapDecompiler.FaceToBrushDecompilation;
using HalfLife.UnifiedSdk.MapDecompiler.TreeDecompilation;
using System.Collections.Immutable;

namespace HalfLife.UnifiedSdk.MapDecompiler
{
    public static class DecompilerStrategies
    {
        public static IDecompilerStrategy TreeDecompilerStrategy { get; } = new TreeDecompilerStrategy();

        public static IDecompilerStrategy FaceToBrushDecompilerStrategy { get; } = new FaceToBrushDecompilerStrategy();

        public static ImmutableArray<IDecompilerStrategy> Strategies { get; } = ImmutableArray.Create(
            TreeDecompilerStrategy,
            FaceToBrushDecompilerStrategy);
    }
}
