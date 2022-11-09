namespace HalfLife.UnifiedSdk.MapDecompiler.FaceToBrushDecompilation
{
    internal sealed class BspSide
    {
        public int PlaneNumber;

        public int Side;

        public int TextureInfo;

        public Winding Winding;

        public BspSide(int planeNumber, int side, int textureInfo, Winding winding)
        {
            PlaneNumber = planeNumber;
            Side = side;
            TextureInfo = textureInfo;
            Winding = winding;
        }

        public BspSide(BspSide other)
        {
            PlaneNumber = other.PlaneNumber;
            Side = other.Side;
            TextureInfo = other.TextureInfo;
            Winding = other.Winding.Clone();
        }

        public BspSide Clone()
        {
            return new BspSide(this);
        }
    }
}
