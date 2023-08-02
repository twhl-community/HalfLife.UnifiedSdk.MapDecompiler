using Sledge.Formats.Bsp;
using BspVersion = Sledge.Formats.Bsp.Version;

namespace HalfLife.UnifiedSdk.MapDecompiler.Serialization
{
    public static class BspSerialization
    {
        public static (BspFile BspFile, bool IsHLAlphaMap) Deserialize(Stream stream)
        {
            // Copy the file and see if it's version 29. If so, switch it to version 30 so HL Alpha maps can load.
            // Note that this will cause Quake maps to load incorrectly. They aren't supported, but people will try.
            var memoryStream = new MemoryStream();

            stream.CopyTo(memoryStream);

            memoryStream.Position = 0;

            var reader = new BinaryReader(memoryStream);

            var magic = (BspVersion)reader.ReadUInt32();

            // Modify 29 to 30. For other versions depend on error handling later on.
            if (magic == BspVersion.Quake1)
            {
                memoryStream.Position = 0;

                var writer = new BinaryWriter(memoryStream);

                writer.Write((uint)BspVersion.Goldsource);
            }

            if (magic != BspVersion.Goldsource)
            {
                if (!Enum.IsDefined(magic))
                {
                    // This may be a Source map which has a 4 byte id before the version.
                    // Read the whole thing as a 64 bit integer instead.
                    memoryStream.Position = 0;

                    var version = (BspVersion)reader.ReadUInt64();

                    if (Enum.IsDefined(version))
                    {
                        magic = version;
                    }
                }

                throw new NotSupportedException($"Cannot decompile version {magic} BSP files");
            }

            memoryStream.Position = 0;

            return (new BspFile(memoryStream), magic == BspVersion.Quake1);
        }
    }
}
