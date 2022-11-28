using Serilog;
using Sledge.Formats.Bsp;
using Sledge.Formats.Map.Objects;

namespace HalfLife.UnifiedSdk.MapDecompiler.FaceToBrushDecompilation
{
    internal sealed class FaceToBrushDecompilerStrategy : DecompilerStrategy
    {
        public override string Name => "FaceToBrush";

        public override MapFile Decompile(ILogger logger, BspFile bspFile, DecompilerOptions options, CancellationToken cancellationToken)
        {
            return FaceToBrushDecompiler.Decompile(logger, bspFile, options, cancellationToken);
        }
    }
}
