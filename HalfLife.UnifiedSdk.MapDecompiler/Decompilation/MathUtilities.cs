using Sledge.Formats.Bsp.Objects;
using System.Numerics;
using BspPlane = Sledge.Formats.Bsp.Objects.Plane;

namespace HalfLife.UnifiedSdk.MapDecompiler.Decompilation
{
    internal static class MathUtilities
    {
        public const float NormalEpsilon = 0.0001f;
        public const float DistEpsilon = 0.02f;

        public static void ClearBounds(ref Vector3 mins, ref Vector3 maxs)
        {
            // TODO use proper constants
            mins.X = mins.Y = mins.Z = 99999;
            maxs.X = maxs.Y = maxs.Z = -99999;
        }

        public static void AddPointToBounds(Vector3 v, ref Vector3 mins, ref Vector3 maxs)
        {
            mins = Vector3.Min(v, mins);
            maxs = Vector3.Max(v, maxs);
        }

        public static float RoundInt(float input)
        {
            return MathF.Floor(input + 0.5f);
        }

        public static void SnapVector(ref Vector3 normal)
        {
            static bool Snap(ref Vector3 normal, ref float normalValue)
            {
                if (MathF.Abs(normalValue - 1) < NormalEpsilon)
                {
                    normal = Vector3.Zero;
                    normalValue = 1;
                    return true;
                }

                if (MathF.Abs(normalValue - -1) < NormalEpsilon)
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

        public static void SnapPlane(Vector3 normal, ref float dist)
        {
            SnapVector(ref normal);

            if (MathF.Abs(dist - RoundInt(dist)) < DistEpsilon)
            {
                dist = RoundInt(dist);
            }
        }

        public static bool PlaneEqual(BspPlane p, Vector3 normal, float dist)
        {
            var diff = p.Normal - normal;

            return MathF.Abs(diff.X) < NormalEpsilon
                && MathF.Abs(diff.Y) < NormalEpsilon
                && MathF.Abs(diff.Z) < NormalEpsilon
                && MathF.Abs(p.Distance - dist) < DistEpsilon;
        }

        public static PlaneType PlaneTypeForNormal(Vector3 normal)
        {
            // NOTE: should these have an epsilon around 1.0?		
            if (normal.X == 1.0 || normal.X == -1.0)
                return PlaneType.X;
            if (normal.Y == 1.0 || normal.Y == -1.0)
                return PlaneType.Y;
            if (normal.Z == 1.0 || normal.Z == -1.0)
                return PlaneType.Z;

            var ax = MathF.Abs(normal.X);
            var ay = MathF.Abs(normal.Y);
            var az = MathF.Abs(normal.Z);

            if (ax >= ay && ax >= az)
                return PlaneType.AnyX;
            if (ay >= ax && ay >= az)
                return PlaneType.AnyY;
            return PlaneType.AnyZ;
        }

        public static PlaneSide BrushMostlyOnSide(BspBrush brush, BspPlane plane)
        {
            float max = 0;
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
                    var d = Vector3.Dot(point, plane.Normal) - plane.Distance;

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
