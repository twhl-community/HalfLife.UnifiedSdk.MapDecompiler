namespace HalfLife.UnifiedSdk.MapDecompiler.TreeDecompilation
{
    internal partial class TreeDecompiler
    {
        private List<BspBrush> MergeBrushes(List<BspBrush> brushlist, int modelNumber)
        {
            int nummerges = 0;

            List<BspBrush> newbrushlist = new();

            int merged;

            do
            {
                _cancellationToken.ThrowIfCancellationRequested();

                merged = 0;
                newbrushlist.Clear();

                for (int i = 0; i < brushlist.Count; i = 0)
                {
                    var b1 = brushlist[i];

                    bool mergedCurrent = false;

                    for (int j = i + 1; j < brushlist.Count; ++j)
                    {
                        var newbrush = TryMergeBrushes(b1, brushlist[j]);

                        //if a merged brush is created
                        if (newbrush is not null)
                        {
                            brushlist.Add(newbrush);
                            brushlist.RemoveAt(j);
                            brushlist.RemoveAt(i);

                            ++nummerges;
                            ++merged;
                            mergedCurrent = true;

                            break;
                        }
                    }
                    //Keep b1 if it can't be merged with any of the other brushes
                    if (!mergedCurrent)
                    {
                        brushlist.Remove(b1);
                        newbrushlist.Add(b1);
                    }
                }

                (brushlist, newbrushlist) = (newbrushlist, brushlist);
            } while (merged > 0);

            if (modelNumber == 0) _logger.Information("{Count} brushes merged", nummerges);

            return brushlist;
        }

        private BspBrush? TryMergeBrushes(BspBrush brush1, BspBrush brush2)
        {
            //can't merge brushes with different contents
            if (brush1.Side != brush2.Side)
            {
                return null;
            }

            //check for bounding box overlapp
            for (int i = 0; i < 3; ++i)
            {
                if (Vector3Utils.GetByIndex(ref brush1.Mins, i) > (Vector3Utils.GetByIndex(ref brush2.Maxs, i) + 2)
                    || Vector3Utils.GetByIndex(ref brush1.Maxs, i) < (Vector3Utils.GetByIndex(ref brush2.Mins, i) - 2))
                {
                    //_logger.Verbose("TryMergeBrushes: Rejecting brushes due to overlap");
                    return null;
                }
            }

            int shared = 0;
            //check if the brush is convex... flipped planes make a brush non-convex
            foreach (var side1 in brush1.Sides)
            {
                //don't check the "shared" sides
                if (brush2.Sides.Find(s => side1.PlaneNumber == (s.PlaneNumber ^ 1)) != null)
                {
                    ++shared;

                    //there may only be ONE shared side
                    if (shared > 1)
                    {
                        //_logger.Verbose("TryMergeBrushes: Rejecting brushes due to multiple shared sides");
                        return null;
                    }

                    continue;
                }

                foreach (var side2 in brush2.Sides)
                {
                    //don't check the "shared" sides
                    if (brush1.Sides.Find(s => s.PlaneNumber == (side2.PlaneNumber ^ 1)) != null)
                        continue;

                    //if the side is in the same plane
                    if (side1.PlaneNumber == side2.PlaneNumber)
                    {
                        if (side1.TextureInfo != TexInfoNode
                            && side2.TextureInfo != TexInfoNode
                            && side1.TextureInfo != side2.TextureInfo)
                        {
                            //_logger.Verbose("TryMergeBrushes: Rejecting brushes due to mismatched textures");
                            return null;
                        }

                        continue;
                    }

                    var plane1 = _bspPlanes[side1.PlaneNumber];
                    var plane2 = _bspPlanes[side2.PlaneNumber];

                    if (Winding.AreNonConvex(side1.Winding, side2.Winding,
                        plane1.Normal, plane2.Normal,
                        plane1.Distance, plane2.Distance))
                    {
                        //_logger.Verbose("TryMergeBrushes: Rejecting brushes due to resulting brush being concave");
                        return null;
                    }
                }
            }

            var newbrush = new BspBrush(brush1.Sides.Count + brush2.Sides.Count);

            //fix texinfos for sides lying in the same plane
            foreach (var side1 in brush1.Sides)
            {
                foreach (var side2 in brush2.Sides)
                {
                    //if both sides are in the same plane get the texinfo right
                    if (side1.PlaneNumber == side2.PlaneNumber)
                    {
                        if (side1.TextureInfo == TexInfoNode) side1.TextureInfo = side2.TextureInfo;
                        if (side2.TextureInfo == TexInfoNode) side2.TextureInfo = side1.TextureInfo;
                    }
                }
            }

            foreach (var side1 in brush1.Sides)
            {
                //don't add the "shared" sides
                if (brush2.Sides.Find(s => side1.PlaneNumber == (s.PlaneNumber ^ 1)) != null)
                    continue;

                if (newbrush.Sides.Find(cs => cs.PlaneNumber == side1.PlaneNumber) != null)
                {
                    _logger.Information("brush duplicate plane");
                    continue;
                }

                //add this side
                newbrush.Sides.Add(side1.Clone());
            }

            foreach (var side2 in brush2.Sides)
            {
                if (brush1.Sides.Find(side1 =>
                {
                    //if the side is in the same plane
                    if (side2.PlaneNumber == side1.PlaneNumber) return true;
                    //don't add the "shared" sides
                    if (side2.PlaneNumber == (side1.PlaneNumber ^ 1)) return true;

                    return false;
                }) != null)
                {
                    continue;
                }

                if (newbrush.Sides.Find(cs => cs.PlaneNumber == side2.PlaneNumber) != null)
                {
                    _logger.Information("brush duplicate plane");
                    continue;
                }

                //add this side
                newbrush.Sides.Add(side2.Clone());
            }

            BSPBrushWindings(newbrush);
            MathUtils.BoundBrush(newbrush);
            CheckBSPBrush(newbrush);

            newbrush.Side = brush1.Side;

            return newbrush;
        }

        private void BSPBrushWindings(BspBrush brush)
        {
            foreach (var side1 in brush.Sides)
            {
                var plane = _bspPlanes[side1.PlaneNumber];

                var w = Winding.BaseWindingForPlane(plane.Normal, plane.Distance);

                foreach (var side2 in brush.Sides)
                {
                    if (side1 == side2)
                    {
                        continue;
                    }

                    plane = _bspPlanes[side2.PlaneNumber ^ 1];

                    w = w.ChopWindingInPlace(plane.Normal, plane.Distance, 0); //CLIP_EPSILON);

                    if (w is null)
                    {
                        break;
                    }
                }

                side1.Winding = w;
            }
        }

        void CheckBSPBrush(BspBrush brush)
        {
            //check if the brush is convex... flipped planes make a brush non-convex
            foreach (var side1 in brush.Sides)
            {
                foreach (var side2 in brush.Sides)
                {
                    if (side1 == side2)
                        continue;

                    var plane1 = _bspPlanes[side1.PlaneNumber];
                    var plane2 = _bspPlanes[side2.PlaneNumber];

                    if (Winding.AreNonConvex(side1.Winding,
                                            side2.Winding,
                                            plane1.Normal, plane2.Normal,
                                            plane1.Distance, plane2.Distance))
                    {
                        _logger.Information("non convex brush");
                        break;
                    }
                }
            }

            MathUtils.BoundBrush(brush);

            //check for out of bound brushes
            bool CheckBounds(int i, double min, double max)
            {
                if (min < -Winding.BogusRange || max > Winding.BogusRange)
                {
                    _logger.Information("brush: bounds out of range");
                    _logger.Information("ob.Mins[{Index}] = {Min}, obMaxs[{Index2}] = {Max}", i, min, i, max);
                    return false;
                }
                if (min > Winding.BogusRange || max < -Winding.BogusRange)
                {
                    _logger.Information("brush: no visible sides on brush");
                    _logger.Information("ob.Mins[{Index}] = {Min}, ob.Maxs[{Index2}] = {Max}", i, min, i, max);
                    return false;
                }

                return true;
            }

            if (!CheckBounds(0, brush.Mins.X, brush.Maxs.X)
                || !CheckBounds(1, brush.Mins.Y, brush.Maxs.Y)
                || !CheckBounds(2, brush.Mins.Z, brush.Maxs.Z))
            {
                // Nothing
            }
        }
    }
}
