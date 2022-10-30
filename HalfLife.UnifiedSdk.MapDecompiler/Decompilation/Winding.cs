﻿using System.Numerics;

namespace HalfLife.UnifiedSdk.MapDecompiler.Decompilation
{
    internal sealed class Winding : ICloneable
    {
        private const int BogusRange = 65535;

        /// <summary>
        /// somewhere outside the map
        /// </summary>
        private const int BogusMapRange = BogusRange + 128;

        private const int MaxPointsOnWinding = 96;
        private const float ConvexEpsilon = 0.2f;
        private const float EdgeLength = 0.2f;

        private const int SideFront = 0;
        private const int SideBack = 1;
        private const int SideOn = 2;

        public List<Vector3> Points { get; }

        public Winding(int points)
        {
            Points = new(points);
        }

        public Winding(Winding other)
        {
            Points = other.Points.ToList();
        }

        public Winding Clone()
        {
            return new Winding(this);
        }

        public float Area()
        {
            float total = 0;

            for (int i = 2; i < Points.Count; ++i)
            {
                var d1 = Points[i - 1] - Points[0];
                var d2 = Points[i] - Points[0];

                var cross = Vector3.Cross(d1, d2);

                total += 0.5f * cross.Length();
            }

            return total;
        }

        public bool IsTiny()
        {
            int edges = 0;

            for (int i = 0; i < Points.Count; ++i)
            {
                int j = i == Points.Count - 1 ? 0 : i + 1;
                var delta = Points[j] - Points[i];
                float len = delta.Length();

                if (len > EdgeLength && ++edges == 3)
                {
                    return false;
                }
            }

            return true;
        }

        public bool IsHuge()
        {
            foreach (var point in Points)
            {
                if (point.X < -BogusMapRange + 1 || point.X > BogusMapRange - 1
                    || point.Y < -BogusMapRange + 1 || point.Y > BogusMapRange - 1
                    || point.Z < -BogusMapRange + 1 || point.Z > BogusMapRange - 1)
                {
                    return true;
                }
            }

            return false;
        }

        public void ClipEpsilon(Vector3 normal, float dist, float epsilon, out Winding? front, out Winding? back)
        {
            Span<float> dists = stackalloc float[MaxPointsOnWinding + 4];
            Span<int> sides = stackalloc int[MaxPointsOnWinding + 4];
            Span<int> counts = stackalloc int[3];

            int i;

            // determine sides for each point
            for (i = 0; i < Points.Count; ++i)
            {
                var dot = Vector3.Dot(Points[i], normal);
                dot -= dist;
                dists[i] = dot;

                if (dot > epsilon)
                {
                    sides[i] = SideFront;
                }
                else if (dot < -epsilon)
                {
                    sides[i] = SideBack;
                }
                else
                {
                    sides[i] = SideOn;
                }

                ++counts[sides[i]];
            }

            sides[i] = sides[0];
            dists[i] = dists[0];

            front = back = null;

            if (counts[0] == 0)
            {
                back = new Winding(this);
                return;
            }

            if (counts[1] == 0)
            {
                front = new Winding(this);
                return;
            }

            int maxpts = Points.Count + 4;  // cant use counts[0]+2 because
                                            // of fp grouping errors

            var f = front = new Winding(maxpts);
            var b = back = new Winding(maxpts);

            for (i = 0; i < Points.Count; ++i)
            {
                var p1 = Points[i];

                if (sides[i] == SideOn)
                {
                    f.Points.Add(p1);
                    b.Points.Add(p1);
                    continue;
                }

                if (sides[i] == SideFront)
                {
                    f.Points.Add(p1);
                }
                else if (sides[i] == SideBack)
                {
                    b.Points.Add(p1);
                }

                if (sides[i + 1] == SideOn || sides[i + 1] == sides[i])
                {
                    continue;
                }

                // generate a split point
                var p2 = Points[(i + 1) % Points.Count];

                var dot = dists[i] / (dists[i] - dists[i + 1]);

                Vector3 mid = new(
                    GetMid(dist, dot, normal.X, p1.X, p2.X),
                    GetMid(dist, dot, normal.Y, p1.Y, p2.Y),
                    GetMid(dist, dot, normal.Z, p1.Z, p2.Z)
                    );

                f.Points.Add(mid);
                b.Points.Add(mid);
            }

            if (f.Points.Count > maxpts || b.Points.Count > maxpts)
            {
                throw new InvalidOperationException("ClipWinding: points exceeded estimate");
            }

            if (f.Points.Count > MaxPointsOnWinding || b.Points.Count > MaxPointsOnWinding)
            {
                throw new InvalidOperationException("ClipWinding: MAX_POINTS_ON_WINDING");
            }
        }

        public static Winding BaseWindingForPlane(Vector3 normal, float dist)
        {
            // find the major axis
            int x = -1;
            float max = -BogusRange;

            static void DetermineAxis(ref int axisIndex, ref float axisMax, float normal, int index)
            {
                var v = Math.Abs(normal);
                if (v > axisMax)
                {
                    axisIndex = index;
                    axisMax = v;
                }
            }

            DetermineAxis(ref x, ref max, normal.X, 0);
            DetermineAxis(ref x, ref max, normal.Y, 1);
            DetermineAxis(ref x, ref max, normal.Z, 2);

            if (x == -1)
            {
                throw new InvalidOperationException("BaseWindingForPlane: no axis found");
            }

            var vup = Vector3.Zero;

            if (x == 2)
            {
                vup.X = 1;
            }
            else
            {
                vup.Z = 1;
            }

            var v = Vector3.Dot(vup, normal);
            vup = Vector3.Normalize(vup + -v * normal);

            var org = normal * dist;

            var vright = Vector3.Cross(vup, normal);

            vup *= BogusRange;
            vright *= BogusRange;

            // project a really big	axis aligned box onto the plane
            Winding w = new(4);

            w.Points.Add(org - vright);
            w.Points[0] += vup;

            w.Points.Add(org + vright);
            w.Points[1] += vup;

            w.Points.Add(org + vright);
            w.Points[2] -= vup;

            w.Points.Add(org - vright);
            w.Points[3] -= vup;

            return w;
        }

        public Winding? ChopWindingInPlace(Vector3 normal, float dist, float epsilon)
        {
            Span<float> dists = stackalloc float[MaxPointsOnWinding + 4];
            Span<int> sides = stackalloc int[MaxPointsOnWinding + 4];
            Span<int> counts = stackalloc int[3];

            int i;

            // determine sides for each point
            for (i = 0; i < Points.Count; ++i)
            {
                float dot = Vector3.Dot(Points[i], normal);
                dot -= dist;
                dists[i] = dot;

                if (dot > epsilon)
                {
                    sides[i] = SideFront;
                }
                else if (dot < -epsilon)
                {
                    sides[i] = SideBack;
                }
                else
                {
                    sides[i] = SideOn;
                }

                ++counts[sides[i]];
            }

            sides[i] = sides[0];
            dists[i] = dists[0];

            if (counts[0] == 0)
            {
                return null;
            }

            if (counts[1] == 0)
            {
                return this;     // stays the same
            }

            int maxpts = Points.Count + 4;    // cant use counts[0]+2 because
                                              // of fp grouping errors

            var f = new Winding(maxpts);

            for (i = 0; i < Points.Count; ++i)
            {
                var p1 = Points[i];

                if (sides[i] == SideOn)
                {
                    f.Points.Add(p1);
                    continue;
                }

                if (sides[i] == SideFront)
                {
                    f.Points.Add(p1);
                }

                if (sides[i + 1] == SideOn || sides[i + 1] == sides[i])
                    continue;

                // generate a split point
                var p2 = Points[(i + 1) % Points.Count];

                var dot = dists[i] / (dists[i] - dists[i + 1]);

                Vector3 mid = new(
                    GetMid(dist, dot, normal.X, p1.X, p2.X),
                    GetMid(dist, dot, normal.Y, p1.Y, p2.Y),
                    GetMid(dist, dot, normal.Z, p1.Z, p2.Z)
                    );

                f.Points.Add(mid);
            }

            if (f.Points.Count > maxpts)
            {
                throw new InvalidOperationException("ClipWinding: points exceeded estimate");
            }

            if (f.Points.Count > MaxPointsOnWinding)
            {
                throw new InvalidOperationException("ClipWinding: MAX_POINTS_ON_WINDING");
            }

            return f;
        }

        public static bool AreNonConvex(Winding? w1, Winding? w2,
                             Vector3 normal1, Vector3 normal2,
                             float dist1, float dist2)
        {
            if (w1 is null || w2 is null)
            {
                return false;
            }

            //check if one of the points of face1 is at the back of the plane of face2
            for (int i = 0; i < w1.Points.Count; ++i)
            {
                if (Vector3.Dot(normal2, w1.Points[i]) - dist2 > ConvexEpsilon)
                {
                    return true;
                }
            }

            //check if one of the points of face2 is at the back of the plane of face1
            for (int i = 0; i < w2.Points.Count; ++i)
            {
                if (Vector3.Dot(normal1, w2.Points[i]) - dist1 > ConvexEpsilon)
                {
                    return true;
                }
            }

            return false;
        }

        private static float GetMid(float dist, float dot, float normal, float p1, float p2)
        {
            // avoid round off error when possible
            return normal switch
            {
                1 => dist,
                -1 => -dist,
                _ => p1 + dot * (p2 - p1)
            };
        }

        object ICloneable.Clone()
        {
            return Clone();
        }
    }
}
