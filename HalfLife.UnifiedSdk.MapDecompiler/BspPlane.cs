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
            var normal = plane.Normal.ToDouble();
            // Fix rounding errors.
            Vector3Utils.RoundNormal(ref normal);
            Normal = normal;
            Distance = plane.Distance;
            // Calculate correct plane type (in case normal was rounded).
            Type = Vector3Utils.PlaneTypeForNormal(normal);
        }
    }
}
