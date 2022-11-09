using Sledge.Formats.Bsp.Objects;

namespace HalfLife.UnifiedSdk.MapDecompiler
{
    internal static class BspPlaneExtensions
    {
        /// <summary>
        /// Returns a copy of the plane with the normal and distance inverted.
        /// </summary>
        public static BspPlane ToInverted(this BspPlane plane)
        {
            return new()
            {
                Normal = -plane.Normal,
                Distance = -plane.Distance,
                Type = plane.Type
            };
        }

        /// <summary>
        /// Checks if an axial plane is facing in a negative direction.
        /// </summary>
        public static bool IsFacingNegative(this BspPlane plane)
        {
            return plane.Type <= PlaneType.Z && (plane.Normal.X < 0
                    || plane.Normal.Y < 0
                    || plane.Normal.Z < 0);
        }
    }
}
