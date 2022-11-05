using Sledge.Formats.Id;
using Sledge.Formats.Texture.Wad;
using Sledge.Formats.Texture.Wad.Lumps;

namespace HalfLife.UnifiedSdk.MapDecompiler.Jobs
{
    /// <summary>
    /// Custom lump implementation to allow creation of mip texture lumps.
    /// TODO: open issue to add this to Sledge.Formats.Texture.
    /// </summary>
    internal sealed class JobMipTextureLump : MipTexture, ILump
    {
        public LumpType Type => LumpType.MipTexture;

        public int Write(BinaryWriter bw)
        {
            var pos = bw.BaseStream.Position;
            Write(bw, true, this);
            return (int)(bw.BaseStream.Position - pos);
        }
    }
}
