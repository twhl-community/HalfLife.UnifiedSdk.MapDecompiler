using HalfLife.UnifiedSdk.MapDecompiler.Decompilation;
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

        public MapDecompilerJobStatus Decompile(MapDecompilerJob job, DecompilerOptions decompilerOptions, CancellationToken cancellationToken)
        {
            _stopWatch.Restart();

            var status = MapDecompilerJobStatus.Failed;

            try
            {
                // If we were already cancelled.
                cancellationToken.ThrowIfCancellationRequested();

                var (bspFile, mapFile) = DecompileBSPFile(job, decompilerOptions, cancellationToken);
                WriteMapFile(job, mapFile);
                MaybeWriteWadFile(job, bspFile);
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

        private (BspFile, MapFile) DecompileBSPFile(MapDecompilerJob job, DecompilerOptions decompilerOptions, CancellationToken cancellationToken)
        {
            job.Logger.Information("Loading map from {BspFileName}", job.BspFileName);

            using var stream = File.OpenRead(job.BspFileName);

            job.Logger.Information("Deserializing BSP data");

            var bspFile = new BspFile(stream);

            LogTimeElapsed(job.Logger);

            job.Logger.Information("Decompiling map");

            var mapFile = Decompiler.Decompile(job.Logger, bspFile, decompilerOptions, cancellationToken);

            LogTimeElapsed(job.Logger);

            return (bspFile, mapFile);
        }

        private void WriteMapFile(MapDecompilerJob job, MapFile mapFile)
        {
            job.Logger.Information("Writing {MapFileName}", job.MapFileName);

            using var output = File.Open(job.MapFileName, FileMode.Create);

            MapSerialization.SerializeMap(_format, output, mapFile);

            LogTimeElapsed(job.Logger);
        }

        private void MaybeWriteWadFile(MapDecompilerJob job, BspFile bspFile)
        {
            if (bspFile.Textures.Any(t => t.NumMips > 0))
            {
                var wadFileName = job.GetOutputFileName(MapDecompilerJobConstants.WadExtension, "{0}_generated");

                job.Logger.Information("Writing {WadFileName}", wadFileName);

                // Map has at least one embedded texture, create wad file.
                WadFile wadFile = new(WadVersion.Wad3);

                foreach (var texture in bspFile.Textures.Where(t => t.NumMips > 0))
                {
                    job.Logger.Information("Adding texture {Name}", texture.Name);

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

                using var stream = File.Open(wadFileName, FileMode.Create);

                wadFile.Write(stream);

                LogTimeElapsed(job.Logger);
            }
        }
    }
}
