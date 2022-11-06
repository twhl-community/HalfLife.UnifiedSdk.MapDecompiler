using Serilog;
using Sledge.Formats.Bsp;
using Sledge.Formats.Map.Objects;

namespace HalfLife.UnifiedSdk.MapDecompiler
{
    /// <summary>
    /// Represents a particular decompiler algorithm.
    /// </summary>
    public abstract class DecompilerStrategy
    {
        /// <summary>
        /// Name used to select strategies.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Decompiles the given BSP file and returns a map file that represents the decompiled output.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="bspFile"></param>
        /// <param name="options"></param>
        /// <param name="cancellationToken"></param>
        public abstract MapFile Decompile(ILogger logger, BspFile bspFile, DecompilerOptions options, CancellationToken cancellationToken);

        // Used to print the strategy name on the command line.
        public override string ToString()
        {
            return Name;
        }
    }
}
