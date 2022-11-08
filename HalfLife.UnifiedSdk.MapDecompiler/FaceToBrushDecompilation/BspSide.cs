namespace HalfLife.UnifiedSdk.MapDecompiler.FaceToBrushDecompilation
{
    internal sealed class BspSide
    {
        public int PlaneNumber;

        public int TextureInfo;

        public Winding Winding;

        public BspSide(int planeNumber, int textureInfo, Winding winding)
        {
            PlaneNumber = planeNumber;
            TextureInfo = textureInfo;
            Winding = winding;
        }

        public BspSide(BspSide other)
        {
            PlaneNumber = other.PlaneNumber;
            TextureInfo = other.TextureInfo;
            Winding = other.Winding.Clone();
        }

        public BspSide Clone()
        {
            return new BspSide(this);
        }
    }
}
