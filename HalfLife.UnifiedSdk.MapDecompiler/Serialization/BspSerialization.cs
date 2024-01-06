using Sledge.Formats.Bsp;
using System.Text;
using BspVersion = Sledge.Formats.Bsp.Version;

namespace HalfLife.UnifiedSdk.MapDecompiler.Serialization
{
    public static class BspSerialization
    {
        /// <summary>
        /// See below for what this value means.
        /// </summary>
        private const int BspIsXmlFileVersion = 1329865020;

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
                magic = BspVersion.Goldsource;
            }

            if (magic != BspVersion.Goldsource)
            {
                var additionalComments = string.Empty;

                if ((int)magic == BspIsXmlFileVersion)
                {
                    additionalComments = @"
This is actually an XML file, probably an HTML file like an error page (starts with ""<!DOCTYPE html>"")
downloaded from a server's FastDL host describing why it is rejecting the attempt to download files";
                }
                else if (!Enum.IsDefined(magic))
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

                memoryStream.Position = 0;

                var firstBytes = new byte[64];

                var bytesRead = memoryStream.Read(firstBytes, 0, firstBytes.Length);

                var firstBytesAsUTF8 = Encoding.UTF8.GetString(firstBytes, 0, bytesRead);

                throw new NotSupportedException(
                    $@"Cannot decompile version {magic} BSP files
First {bytesRead} bytes converted to UTF-8 are:

""{firstBytesAsUTF8}""
{additionalComments}");
            }

            memoryStream.Position = 0;

            return (new BspFile(memoryStream), magic == BspVersion.Quake1);
        }
    }
}
