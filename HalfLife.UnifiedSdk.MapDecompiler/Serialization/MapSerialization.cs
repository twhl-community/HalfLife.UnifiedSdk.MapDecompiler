using Sledge.Formats.Map.Formats;
using Sledge.Formats.Map.Objects;
using System.Diagnostics;

namespace HalfLife.UnifiedSdk.MapDecompiler.Serialization
{
    public static class MapSerialization
    {
        public static string Version { get; } = FileVersionInfo.GetVersionInfo(typeof(MapSerialization).Assembly.Location).FileVersion ?? "Unknown version";

        public static void SerializeMap(IMapFormat format, Stream stream, MapFile mapFile)
        {
            ArgumentNullException.ThrowIfNull(format);
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(mapFile);

            {
                using var writer = new StreamWriter(stream, leaveOpen: true);

                writer.WriteLine("//=====================================================");
                writer.WriteLine("//");
                writer.WriteLine($"// map file created with HalfLife.UnifiedSdk.MapDecompiler {Version}");
                writer.WriteLine("//");
                writer.WriteLine("// MapDecompiler is designed to decompile material in which you own the copyright");
                writer.WriteLine("// or have obtained permission to decompile from the copyright owner. Unless");
                writer.WriteLine("// you own the copyright or have permission to decompile from the copyright");
                writer.WriteLine("// owner, you may be violating copyright law and be subject to payment of");
                writer.WriteLine("// damages and other remedies. If you are uncertain about your rights, contact");
                writer.WriteLine("// your legal advisor.");
                writer.WriteLine("//");
                writer.WriteLine("//");
                writer.WriteLine("//=====================================================");
            }

            format.Write(stream, mapFile, "Worldcraft");
        }
    }
}
