namespace HalfLife.UnifiedSdk.MapDecompiler
{
    internal sealed class Winding : ICloneable
    {
        private const int BogusRange = 65535;

        /// <summary>
        /// somewhere outside the map
        /// </summary>
        private const int BogusMapRange = BogusRange + 128;

        private const int MaxPointsOnWinding = 96;
        private const double ConvexEpsilon = 0.2;
        private const double EdgeLength = 0.2;

        private const int SideFront = 0;
        private const int SideBack = 1;
        private const int SideOn = 2;

        public List<Vector3> Points { get; }

        public Winding(int points)
        {
            Points = new(points);
        }

        public Winding(List<Vector3> points)
        {
            Points = points;
        }

        public Winding(Winding other)
        {
            Points = other.Points.ToList();
        }

        public Winding Clone()
        {
            return new Winding(this);
        }

        public double Area()
        {
            double total = 0;

            for (int i = 2; i < Points.Count; ++i)
            {
                var d1 = Points[i - 1] - Points[0];
                var d2 = Points[i] - Points[0];

                var cross = Vector3D.Cross(d1, d2);

                total += 0.5 * cross.Length;
            }

            return total;
        }

        /// <summary>
        /// Returns true if the winding would be crunched out of existence by the vertex snapping.
        /// </summary>
        public bool IsTiny()
        {
            int edges = 0;

            for (int i = 0; i < Points.Count; ++i)
            {
                int j = i == Points.Count - 1 ? 0 : i + 1;
                var delta = Points[j] - Points[i];
                var len = delta.Length;

                if (len > EdgeLength && ++edges == 3)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns true if the winding still has one of the points from basewinding for plane
        /// </summary>
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

        public void ClipEpsilon(Vector3 normal, double dist, double epsilon, out Winding? front, out Winding? back)
        {
            Span<double> dists = stackalloc double[MaxPointsOnWinding + 4];
            Span<int> sides = stackalloc int[MaxPointsOnWinding + 4];
            Span<int> counts = stackalloc int[3];

            int i;

            // determine sides for each point
            for (i = 0; i < Points.Count; ++i)
            {
                var dot = Vector3D.Dot(Points[i], normal);
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

        public static Winding BaseWindingForPlane(Vector3 normal, double dist)
        {
            // find the major axis
            int x = -1;
            double max = -BogusRange;

            static void DetermineAxis(ref int axisIndex, ref double axisMax, double normal, int index)
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

            var v = Vector3D.Dot(vup, normal);
            vup = Vector3D.Normalize(vup + (-v * normal));

            var org = normal * dist;

            var vright = Vector3D.Cross(vup, normal);

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

        public Winding? ChopWindingInPlace(Vector3 normal, double dist, double epsilon)
        {
            Span<double> dists = stackalloc double[MaxPointsOnWinding + 4];
            Span<int> sides = stackalloc int[MaxPointsOnWinding + 4];
            Span<int> counts = stackalloc int[3];

            int i;

            // determine sides for each point
            for (i = 0; i < Points.Count; ++i)
            {
                var dot = Vector3D.Dot(Points[i], normal);
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

        public static Winding RemoveCollinearPoints(Winding winding)
        {
            var result = winding.Clone();

            Span<double> lengths = stackalloc double[3];

            bool removed;

            // Check again after the first loop to make sure no new collinear points exist.
            do
            {
                removed = false;

                for (int i = 2; i < result.Points.Count;)
                {
                    var firstVertex = result.Points[i - 2];
                    var secondVertex = result.Points[i - 1];
                    var thirdVertex = result.Points[i];

                    lengths[0] = (firstVertex - secondVertex).Length;
                    lengths[1] = (thirdVertex - secondVertex).Length;
                    lengths[2] = (firstVertex - thirdVertex).Length;

                    lengths.Sort();

                    if (Math.Abs(lengths[2] - (lengths[0] + lengths[1])) <= MathConstants.ContinuousEpsilon)
                    {
                        // Remove middle point.
                        result.Points.RemoveAt(i - 1);
                        removed = true;
                    }
                    else
                    {
                        ++i;
                    }
                }
            }
            while (removed);

            return result;
        }

        public static bool AreNonConvex(Winding? w1, Winding? w2,
                             Vector3 normal1, Vector3 normal2,
                             double dist1, double dist2)
        {
            if (w1 is null || w2 is null)
            {
                return false;
            }

            //check if one of the points of face1 is at the back of the plane of face2
            for (int i = 0; i < w1.Points.Count; ++i)
            {
                if (Vector3D.Dot(normal2, w1.Points[i]) - dist2 > ConvexEpsilon)
                {
                    return true;
                }
            }

            //check if one of the points of face2 is at the back of the plane of face1
            for (int i = 0; i < w2.Points.Count; ++i)
            {
                if (Vector3D.Dot(normal1, w2.Points[i]) - dist1 > ConvexEpsilon)
                {
                    return true;
                }
            }

            return false;
        }

        private static double GetMid(double dist, double dot, double normal, double p1, double p2)
        {
            // avoid round off error when possible
            return normal switch
            {
                1 => dist,
                -1 => -dist,
                _ => p1 + (dot * (p2 - p1))
            };
        }

        object ICloneable.Clone()
        {
            return Clone();
        }
    }
}
