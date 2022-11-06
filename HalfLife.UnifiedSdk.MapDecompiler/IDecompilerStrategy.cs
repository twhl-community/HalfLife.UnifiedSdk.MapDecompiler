using Serilog;
using Sledge.Formats.Bsp;
using Sledge.Formats.Map.Objects;

namespace HalfLife.UnifiedSdk.MapDecompiler
{
    /// <summary>
    /// Represents a particular decompiler algorithm.
    /// </summary>
    public interface IDecompilerStrategy
    {
        /// <summary>
        /// Name used to select strategies.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Decompiles the given BSP file and returns a map file that represents the decompiled output.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="bspFile"></param>
        /// <param name="options"></param>
        /// <param name="cancellationToken"></param>
        MapFile Decompile(ILogger logger, BspFile bspFile, DecompilerOptions options, CancellationToken cancellationToken);
    }
}
