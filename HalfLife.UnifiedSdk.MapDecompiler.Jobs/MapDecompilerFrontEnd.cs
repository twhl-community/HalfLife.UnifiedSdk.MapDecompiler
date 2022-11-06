using HalfLife.UnifiedSdk.MapDecompiler.Serialization;
using Serilog;
using Sledge.Formats.Bsp;
using Sledge.Formats.Map.Formats;
using Sledge.Formats.Map.Objects;
using Sledge.Formats.Texture.Wad;
using System.Diagnostics;
using WadVersion = Sledge.Formats.Texture.Wad.Version;

namespace HalfLife.UnifiedSdk.MapDecompiler.Jobs
{
    /// <summary>
    /// Used to decompile maps in a UI environment. Exceptions are caught and logged and will not be rethrown.
    /// </summary>
    public sealed class MapDecompilerFrontEnd
    {
        private readonly Stopwatch _stopWatch = new();

        private readonly QuakeMapFormat _format = new();

        public MapDecompilerJobStatus Decompile(MapDecompilerJob job, IDecompilerStrategy decompilerStrategy, DecompilerOptions decompilerOptions, CancellationToken cancellationToken)
        {
            const string outputTemplate = "{Message:lj}{NewLine}{Exception}";

            using var logger = new LoggerConfiguration()
                .WriteTo.Sink(new ForwardingSink(job.LogMessage, outputTemplate))
                .WriteTo.File(job.GetOutputFileName(MapDecompilerJobConstants.LogExtension), outputTemplate: outputTemplate)
                .MinimumLevel.Information()
                .CreateLogger();

            _stopWatch.Restart();

            var status = MapDecompilerJobStatus.Failed;

            try
            {
                // If we were already cancelled.
                cancellationToken.ThrowIfCancellationRequested();

                var (bspFile, mapFile) = DecompileBSPFile(logger, job, decompilerStrategy, decompilerOptions, cancellationToken);
                WriteMapFile(logger, job, mapFile);
                MaybeWriteWadFile(logger, job, bspFile);
                status = MapDecompilerJobStatus.Done;
            }
            catch (OperationCanceledException)
            {
                logger.Information("Decompilation cancelled");
                status = MapDecompilerJobStatus.Canceled;
            }
            catch (Exception e)
            {
                logger.Error(e, "An error occurred while decompiling a map");
            }

            _stopWatch.Stop();

            LogTimeElapsed(logger, true);

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

        private (BspFile, MapFile) DecompileBSPFile(
            ILogger logger, MapDecompilerJob job, IDecompilerStrategy decompilerStrategy, DecompilerOptions decompilerOptions, CancellationToken cancellationToken)
        {
            logger.Information("Loading map from {BspFileName}", job.BspFileName);

            using var stream = File.OpenRead(job.BspFileName);

            logger.Information("Deserializing BSP data");

            var bspFile = new BspFile(stream);

            LogTimeElapsed(logger);

            logger.Information("Decompiling map using {DecompilerStrategy} strategy", decompilerStrategy.Name);

            var mapFile = decompilerStrategy.Decompile(logger, bspFile, decompilerOptions, cancellationToken);

            LogTimeElapsed(logger);

            return (bspFile, mapFile);
        }

        private void WriteMapFile(ILogger logger, MapDecompilerJob job, MapFile mapFile)
        {
            logger.Information("Writing {MapFileName}", job.MapFileName);

            using var output = File.Open(job.MapFileName, FileMode.Create);

            MapSerialization.SerializeMap(_format, output, mapFile);

            LogTimeElapsed(logger);
        }

        private void MaybeWriteWadFile(ILogger logger, MapDecompilerJob job, BspFile bspFile)
        {
            if (bspFile.Textures.Any(t => t.NumMips > 0))
            {
                var wadFileName = job.GetOutputFileName(MapDecompilerJobConstants.WadExtension, "{0}_generated");

                logger.Information("Writing {WadFileName}", wadFileName);

                // Map has at least one embedded texture, create wad file.
                WadFile wadFile = new(WadVersion.Wad3);

                foreach (var texture in bspFile.Textures.Where(t => t.NumMips > 0))
                {
                    logger.Information("Adding texture {Name}", texture.Name);

                    JobMipTextureLump lump = new()
                    {
                        Name = texture.Name,
                        Width = texture.Width,
                        Height = texture.Height,
                        NumMips = texture.NumMips,
                        MipData = texture.MipData,
                        Palette = texture.Palette
                    };

                    wadFile.AddLump(texture.Name, lump);
                }

                logger.Information("Added {Count} textures", wadFile.Lumps.Count());

                using var stream = File.Open(wadFileName, FileMode.Create);

                wadFile.Write(stream);

                LogTimeElapsed(logger);
            }
        }
    }
}
