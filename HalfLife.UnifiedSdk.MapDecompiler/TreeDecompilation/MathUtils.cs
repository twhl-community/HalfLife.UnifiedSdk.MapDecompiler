using Sledge.Formats.Bsp.Objects;

namespace HalfLife.UnifiedSdk.MapDecompiler.TreeDecompilation
{
    internal static class MathUtils
    {
        public const double NormalEpsilon = 0.0001;
        public const double DistEpsilon = 0.02;

        public static void ClearBounds(ref Vector3 mins, ref Vector3 maxs)
        {
            mins.X = mins.Y = mins.Z = double.MaxValue;
            maxs.X = maxs.Y = maxs.Z = double.MinValue;
        }

        public static void AddPointToBounds(Vector3 v, ref Vector3 mins, ref Vector3 maxs)
        {
            mins = Vector3D.Min(v, mins);
            maxs = Vector3D.Max(v, maxs);
        }

        public static double RoundInt(double input)
        {
            return Math.Floor(input + 0.5);
        }

        public static void SnapVector(ref Vector3 normal)
        {
            static bool Snap(ref Vector3 normal, ref double normalValue)
            {
                if (Math.Abs(normalValue - 1) < NormalEpsilon)
                {
                    normal = Vector3.Zero;
                    normalValue = 1;
                    return true;
                }

                if (Math.Abs(normalValue - -1) < NormalEpsilon)
                {
                    normal = Vector3.Zero;
                    normalValue = -1;
                    return true;
                }

                return false;
            }

            if (Snap(ref normal, ref normal.X))
            {
                return;
            }

            if (Snap(ref normal, ref normal.Y))
            {
                return;
            }

            Snap(ref normal, ref normal.Z);
        }

        public static void SnapPlane(Vector3 normal, ref double dist)
        {
            SnapVector(ref normal);

            if (Math.Abs(dist - RoundInt(dist)) < DistEpsilon)
            {
                dist = RoundInt(dist);
            }
        }

        public static bool PlaneEqual(BspPlane p, Vector3 normal, double dist)
        {
            var diff = p.Normal - normal;

            return Math.Abs(diff.X) < NormalEpsilon
                && Math.Abs(diff.Y) < NormalEpsilon
                && Math.Abs(diff.Z) < NormalEpsilon
                && Math.Abs(p.Distance - dist) < DistEpsilon;
        }

        public static PlaneSide BrushMostlyOnSide(BspBrush brush, BspPlane plane)
        {
            double max = 0;
            var planeSide = PlaneSide.Front;

            foreach (var side in brush.Sides)
            {
                var w = side.Winding;

                if (w is null)
                {
                    continue;
                }

                foreach (var point in w.Points)
                {
                    var d = Vector3D.Dot(point, plane.Normal) - plane.Distance;

                    if (d > max)
                    {
                        max = d;
                        planeSide = PlaneSide.Front;
                    }

                    if (-d > max)
                    {
                        max = -d;
                        planeSide = PlaneSide.Back;
                    }
                }
            }

            return planeSide;
        }

        /// <summary>
        /// Sets the mins/maxs based on the windings
        /// </summary>
        /// <param name="brush"></param>
        public static void BoundBrush(BspBrush brush)
        {
            ClearBounds(ref brush.Mins, ref brush.Maxs);

            foreach (var side in brush.Sides)
            {
                if (side.Winding is null)
                {
                    continue;
                }

                foreach (var point in side.Winding.Points)
                {
                    AddPointToBounds(point, ref brush.Mins, ref brush.Maxs);
                }
            }
        }
    }
}
