namespace HalfLife.UnifiedSdk.MapDecompiler.TreeDecompilation
{
    /*
     * 
     typedef struct side_s
{
    int				planenum;	// map plane this side is in
    int				texinfo;		// texture reference
    winding_t		*winding;	// winding of this side
    struct side_s	*original;	// bspbrush_t sides will reference the mapbrush_t sides
   int				lightinfo;	// for SIN only
    int				contents;	// from miptex
    int				surf;			// from miptex
    unsigned short flags;		// side flags
} side_t;		//sizeof(side_t) = 36
     */
    internal sealed class BspSide : ICloneable
    {
        public int PlaneNumber;

        public int TextureInfo;

        public Winding? Winding;

        public BspSide? Original;

        public int LightInfo;

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
            Original = other.Original;
            LightInfo = other.LightInfo;
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
