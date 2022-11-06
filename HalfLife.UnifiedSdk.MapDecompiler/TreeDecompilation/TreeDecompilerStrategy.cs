using Serilog;
using Sledge.Formats.Bsp;
using Sledge.Formats.Map.Objects;

namespace HalfLife.UnifiedSdk.MapDecompiler.TreeDecompilation
{
    internal sealed class TreeDecompilerStrategy : DecompilerStrategy
    {
        public override string Name => "Tree";

        public override MapFile Decompile(ILogger logger, BspFile bspFile, DecompilerOptions options, CancellationToken cancellationToken)
        {
            return TreeDecompiler.Decompile(logger, bspFile, options, cancellationToken);
        }
    }
}
