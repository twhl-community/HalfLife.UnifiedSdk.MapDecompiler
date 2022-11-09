using Sledge.Formats.Bsp.Objects;

namespace HalfLife.UnifiedSdk.MapDecompiler.TreeDecompilation
{
    internal partial class TreeDecompiler
    {
        private List<BspBrush> CreateBrushesFromBSP(int modelNumber)
        {
            var model = _bspModels[modelNumber];
            var headnode = _bspNodes[model.HeadNodes[0]];

            //get the mins and maxs of the world
            var mins = new Vector3(headnode.Mins[0], headnode.Mins[1], headnode.Mins[2]);
            var maxs = new Vector3(headnode.Maxs[0], headnode.Maxs[1], headnode.Maxs[2]);

            //enlarge these mins and maxs
            mins -= new Vector3(8);
            maxs += new Vector3(8);

            //NOTE: have to add the BSP tree mins and maxs to the MAP mins and maxs
            MathUtils.AddPointToBounds(mins, ref _mapMins, ref _mapMaxs);
            MathUtils.AddPointToBounds(maxs, ref _mapMins, ref _mapMaxs);

            if (modelNumber == 0)
            {
                _logger.Information("brush size: {Mins} to {Maxs}", _mapMins, _mapMaxs);
            }

            //create one huge brush containing the whole world
            var brush = BrushFromBounds(mins, maxs);
            brush.Mins = mins;
            brush.Maxs = maxs;

            //create the brushes
            //now we've got a list with brushes!
            return CreateBrushes_r(brush, model.HeadNodes[0]);
        }

        /// <summary>
        /// Creates a new axial brush
        /// </summary>
        /// <param name="mins"></param>
        /// <param name="maxs"></param>
        private BspBrush BrushFromBounds(Vector3 mins, Vector3 maxs)
        {
            BspBrush brush = new();

            brush.Sides.Add(new BspSide
            {
                PlaneNumber = FindFloatPlane(Vector3.UnitX, maxs.X)
            });

            brush.Sides.Add(new BspSide
            {
                PlaneNumber = FindFloatPlane(Vector3.UnitY, maxs.Y)
            });

            brush.Sides.Add(new BspSide
            {
                PlaneNumber = FindFloatPlane(Vector3.UnitZ, maxs.Z)
            });

            brush.Sides.Add(new BspSide
            {
                PlaneNumber = FindFloatPlane(-Vector3.UnitX, -mins.X)
            });

            brush.Sides.Add(new BspSide
            {
                PlaneNumber = FindFloatPlane(-Vector3.UnitY, -mins.Y)
            });

            brush.Sides.Add(new BspSide
            {
                PlaneNumber = FindFloatPlane(-Vector3.UnitZ, -mins.Z)
            });

            CreateBrushWindings(brush);

            return brush;
        }

        private List<BspBrush> CreateBrushes_r(BspBrush brush, int nodenum)
        {
            //if it is a leaf
            if (nodenum < 0)
            {
                var leaf = _bspLeaves[(-nodenum) - 1];

                switch (leaf.Contents)
                {
                    case Contents.Empty:
                        {
                            return new();
                        }

                    // TODO: these assigned contents are different values in the original decompiler. It might make a difference.
                    case Contents.Solid:
                    case Contents.Clip:
                    case Contents.Sky:
                    case Contents.Translucent:
                        {
                            brush.Side = Contents.Solid;
                            return new List<BspBrush> { brush };
                        }

                    case Contents.Water:
                        {
                            brush.Side = Contents.Water;
                            return new List<BspBrush> { brush };
                        }

                    case Contents.Slime:
                        {
                            brush.Side = Contents.Slime;
                            return new List<BspBrush> { brush };
                        }

                    case Contents.Lava:
                        {
                            brush.Side = Contents.Lava;
                            return new List<BspBrush> { brush };
                        }

                    //these contents should not be found in the BSP
                    case Contents.Origin:
                    case Contents.Current0:
                    case Contents.Current90:
                    case Contents.Current180:
                    case Contents.Current270:
                    case Contents.CurrentUp:
                    case Contents.CurrentDown:
                        {
                            throw new InvalidOperationException($"CreateBrushes_r: found contents {leaf.Contents} in Half-Life BSP");
                        }

                    default:
                        {
                            throw new InvalidOperationException($"CreateBrushes_r: unknown contents {leaf.Contents} in Half-Life BSP");
                        }
                }
            }
            //if the rest of the tree is solid
            /*if (SolidTree_r(nodenum))
            {
                brush.Side = (int)Contents.Solid;
                return brush;
            } //end if*/

            var plane = _bspPlanes[(int)_bspNodes[nodenum].Plane];
            var planenum = FindFloatPlane(plane.Normal, plane.Distance);

            //split the brush with the node plane
            SplitBrush(brush, planenum, nodenum, out var front, out var back);

            //every node must split the brush in two
            if (front is null || back is null)
            {
                _logger.Information("CreateBrushes_r: WARNING node not splitting brush");
                //return null;
            }

            //create brushes recursively
            var frontList = front is not null ? CreateBrushes_r(front, _bspNodes[nodenum].Children[0]) : null;
            var backList = back is not null ? CreateBrushes_r(back, _bspNodes[nodenum].Children[1]) : null;

            //link the brushes if possible and return them
            if (frontList is not null)
            {
                if (backList is not null)
                {
                    frontList.AddRange(backList);
                }

                return frontList;
            }
            else
            {
                return backList ?? new();
            }
        }
    }
}
