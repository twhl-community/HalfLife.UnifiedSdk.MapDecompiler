using Sledge.Formats.Bsp.Objects;
using System.Numerics;

namespace HalfLife.UnifiedSdk.MapDecompiler.TreeDecompilation
{
    internal sealed class BspBrush
    {
        public Vector3 Mins;

        public Vector3 Maxs;

        public Contents Side;

        public List<BspSide> Sides = new();

        public BspBrush(int numSides = 0)
        {
            Sides.Capacity = numSides;
        }

        public BspBrush(BspBrush other)
        {
            Mins = other.Mins;
            Maxs = other.Maxs;
            Side = other.Side;
            Sides = other.Sides.ConvertAll(s => new BspSide(s));
        }
    }
}
