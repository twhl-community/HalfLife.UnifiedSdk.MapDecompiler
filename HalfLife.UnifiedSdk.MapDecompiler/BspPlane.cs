using Sledge.Formats.Bsp.Objects;

namespace HalfLife.UnifiedSdk.MapDecompiler
{
    internal sealed class BspPlane
    {
        public Vector3 Normal { get; set; }

        public double Distance { get; set; }

        public PlaneType Type { get; set; }

        public BspPlane()
        {
        }

        public BspPlane(Sledge.Formats.Bsp.Objects.Plane plane)
        {
            Normal = plane.Normal.ToDouble();
            Distance = plane.Distance;
            Type = plane.Type;
        }
    }
}
