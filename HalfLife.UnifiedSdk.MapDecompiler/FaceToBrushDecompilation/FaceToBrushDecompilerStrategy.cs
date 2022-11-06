using Serilog;
using Sledge.Formats.Bsp;
using Sledge.Formats.Map.Objects;

namespace HalfLife.UnifiedSdk.MapDecompiler.FaceToBrushDecompilation
{
    internal sealed class FaceToBrushDecompilerStrategy : IDecompilerStrategy
    {
        public string Name => "FaceToBrush";

        public MapFile Decompile(ILogger logger, BspFile bspFile, DecompilerOptions options, CancellationToken cancellationToken)
        {
            return FaceToBrushDecompiler.Decompile(logger, bspFile, cancellationToken);
        }
    }
}
