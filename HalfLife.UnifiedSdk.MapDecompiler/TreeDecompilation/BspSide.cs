namespace HalfLife.UnifiedSdk.MapDecompiler.TreeDecompilation
{
    internal sealed class BspSide : ICloneable
    {
        public int PlaneNumber;

        public int TextureInfo;

        public Winding? Winding;

        public int Contents;

        public int Surface;

        public SideFlag Flags;

        public BspSide()
        {
        }

        public BspSide(BspSide other)
        {
            PlaneNumber = other.PlaneNumber;
            TextureInfo = other.TextureInfo;
            Winding = other.Winding?.Clone();
            Contents = other.Contents;
            Surface = other.Surface;
            Flags = other.Flags;
        }

        public BspSide Clone()
        {
            return new BspSide(this);
        }

        object ICloneable.Clone()
        {
            return Clone();
        }
    }
}
