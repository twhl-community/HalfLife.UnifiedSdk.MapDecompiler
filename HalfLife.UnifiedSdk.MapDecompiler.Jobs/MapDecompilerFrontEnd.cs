using HalfLife.UnifiedSdk.MapDecompiler.Decompilation;
using HalfLife.UnifiedSdk.MapDecompiler.Serialization;
using Serilog;
using Sledge.Formats.Bsp;
using Sledge.Formats.Map.Formats;
using Sledge.Formats.Map.Objects;
using System.Diagnostics;

namespace HalfLife.UnifiedSdk.MapDecompiler.Jobs
{
    /// <summary>
    /// Used to decompile maps in a UI environment. Exceptions are caught and logged and will not be rethrown.
    /// </summary>
    public sealed class MapDecompilerFrontEnd
    {
        private readonly Stopwatch _stopWatch = new();

        private readonly QuakeMapFormat _format = new();

        public MapDecompilerJobStatus Decompile(MapDecompilerJob job, DecompilerOptions decompilerOptions, CancellationToken cancellationToken)
        {
            _stopWatch.Restart();

            var status = MapDecompilerJobStatus.Failed;

            try
            {
                // If we were already cancelled.
                cancellationToken.ThrowIfCancellationRequested();

                var mapFile = DecompileBSPFile(job, decompilerOptions, cancellationToken);
                WriteMapFile(job, mapFile);
                status = MapDecompilerJobStatus.Done;
            }
            catch (OperationCanceledException)
            {
                job.Logger.Information("Decompilation cancelled");
                status = MapDecompilerJobStatus.Canceled;
            }
            catch (Exception e)
            {
                job.Logger.Error(e, "An error occurred while decompiling a map");
            }

            _stopWatch.Stop();

            LogTimeElapsed(job.Logger, true);

            return status;
        }

        private void LogTimeElapsed(ILogger logger, bool isTotal = false)
        {
            if (isTotal)
            {
                logger.Information("Total time elapsed: {Time:dd\\.hh\\:mm\\:ss\\.fff}", _stopWatch.Elapsed);
            }
            else
            {
                logger.Information("Time elapsed: {Time:dd\\.hh\\:mm\\:ss\\.fff}", _stopWatch.Elapsed);
            }
        }

        private MapFile DecompileBSPFile(MapDecompilerJob job, DecompilerOptions decompilerOptions, CancellationToken cancellationToken)
        {
            job.Logger.Information("Loading map from {BspFileName}", job.BspFileName);

            using var stream = File.OpenRead(job.BspFileName);

            job.Logger.Information("Deserializing BSP data");

            var bspFile = new BspFile(stream);

            LogTimeElapsed(job.Logger);

            job.Logger.Information("Decompiling map");

            var mapFile = Decompiler.Decompile(job.Logger, bspFile, decompilerOptions, cancellationToken);

            LogTimeElapsed(job.Logger);

            return mapFile;
        }

        private void WriteMapFile(MapDecompilerJob job, MapFile mapFile)
        {
            job.Logger.Information("Writing {MapFileName}", job.MapFileName);

            using var output = File.Open(job.MapFileName, FileMode.Create);

            MapSerialization.SerializeMap(_format, output, mapFile);

            LogTimeElapsed(job.Logger);
        }
    }
}
