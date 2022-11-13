using Serilog;
using Sledge.Formats.Bsp;
using Sledge.Formats.Bsp.Lumps;
using Sledge.Formats.Bsp.Objects;
using Sledge.Formats.Map.Objects;
using System.Collections.Immutable;
using System.Diagnostics;
using BspFace = Sledge.Formats.Bsp.Objects.Face;
using BspVersion = Sledge.Formats.Bsp.Version;
using MapEntity = Sledge.Formats.Map.Objects.Entity;

namespace HalfLife.UnifiedSdk.MapDecompiler.TreeDecompilation
{
    internal sealed partial class TreeDecompiler
    {
        private const int PlaneHashes = 1024;
        private const int MaxRange = 4096;
        private const int MaxMapBounds = 65535;

        private const int TexInfoNode = -1; //side is allready on a node

        private readonly ILogger _logger;
        private readonly BspFile _bspFile;
        private readonly DecompilerOptions _options;
        private readonly CancellationToken _cancellationToken;

        private readonly List<BspPlane> _bspPlanes;
        private readonly Faces _bspFaces;
        private readonly Nodes _bspNodes;
        private readonly Texinfo _bspTexInfo;
        private readonly Textures _bspTextures;
        private readonly Surfedges _bspSurfedges;
        private readonly Edges _bspEdges;
        private readonly List<Vector3> _bspVertices;
        private readonly Leaves _bspLeaves;
        private readonly Models _bspModels;

        private Vector3 _mapMins;
        private Vector3 _mapMaxs;

        private readonly HashedPlane[] _planehashes = new HashedPlane[PlaneHashes];

        private readonly ImmutableDictionary<int, string> _textureNameMap;

        private int _numMapBrushes;
        private int _numClipBrushes;

        private readonly int _originTextureIndex;

        private TreeDecompiler(ILogger logger, BspFile bspFile, DecompilerOptions options, CancellationToken cancellationToken)
        {
            _logger = logger;
            _bspFile = bspFile;
            _options = options;
            _cancellationToken = cancellationToken;

            // Cache lumps to avoid lookup overhead.
            //_bspPlanes = _bspFile.Planes;
            _bspFaces = _bspFile.Faces;
            _bspNodes = _bspFile.Nodes;
            _bspTexInfo = _bspFile.Texinfo;
            _bspTextures = _bspFile.Textures;
            _bspSurfedges = _bspFile.Surfedges;
            _bspEdges = _bspFile.Edges;
            _bspVertices = _bspFile.Vertices.Select(v => v.ToDouble()).ToList();
            _bspLeaves = _bspFile.Leaves;
            _bspModels = _bspFile.Models;

            // Rebuild plane list to include both sides of all planes, remap faces to map to correct plane.
            var originalPlanes = _bspFile.Planes.Select(p => new BspPlane(p)).ToList();

            List<BspPlane> planes = new(originalPlanes.Count * 2);

            Dictionary<int, int> remap = new(originalPlanes.Count * 2);

            void AddPlanePair(BspPlane front, BspPlane back, int? frontIndex, int? backIndex)
            {
                // allways put axial planes facing positive first
                if (front.IsFacingNegative())
                {
                    planes.Add(back);
                    planes.Add(front);

                    if (frontIndex is not null)
                    {
                        remap.Add(frontIndex.Value, planes.Count - 1);
                    }

                    if (backIndex is not null)
                    {
                        remap.Add(backIndex.Value, planes.Count - 2);
                    }
                }
                else
                {
                    planes.Add(front);
                    planes.Add(back);

                    if (frontIndex is not null)
                    {
                        remap.Add(frontIndex.Value, planes.Count - 2);
                    }

                    if (backIndex is not null)
                    {
                        remap.Add(backIndex.Value, planes.Count - 1);
                    }
                }
            }

            for (int i = 0; i < originalPlanes.Count;)
            {
                var front = originalPlanes[i];

                if ((i + 1) < originalPlanes.Count)
                {
                    var back = originalPlanes[i + 1];

                    if (front.Distance == -back.Distance && front.Normal == -back.Normal)
                    {
                        // Planes match, this is an existing pair.
                        AddPlanePair(front, back, i, i + 1);

                        i += 2;
                        continue;
                    }
                }

                // Planes don't match or this is the last plane, need to regenerate a back plane.
                AddPlanePair(front, front.ToInverted(), i, null);
                ++i;
            }

            _bspPlanes = planes;

            // Update references to planes.
            foreach (var node in _bspNodes)
            {
                // Every plane should be in remap, so this needs to throw on failure.
                node.Plane = (uint)remap[(int)node.Plane];
            }

            foreach (var face in _bspFaces)
            {
                face.Plane = (ushort)remap[face.Plane];
            }

            // Add existing planes to hash.
            foreach (var planeNumber in Enumerable.Range(0, _bspPlanes.Count))
            {
                AddPlaneToHash(planeNumber);
            }

            // Add some tool textures.
            _originTextureIndex = FindOrCreateTexture("ORIGIN");

            // Cache the texture name lookup map
            _textureNameMap = _bspTexInfo
                .Select((t, i) => new { TexInfo = t, Index = i })
                .ToImmutableDictionary(t => t.Index, t => _bspTextures[t.TexInfo.MipTexture].Name);
        }

        private int FindOrCreateTexture(string name)
        {
            var texture = _bspTextures.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (texture is null)
            {
                texture = new()
                {
                    Name = name,
                };

                _bspTextures.Add(texture);
            }

            return _bspTextures.IndexOf(texture);
        }

        public static MapFile Decompile(ILogger logger, BspFile bspFile, DecompilerOptions options, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(bspFile);
            ArgumentNullException.ThrowIfNull(options);

            if (bspFile.Version != BspVersion.Goldsource)
            {
                throw new ArgumentException("BSP version not supported", nameof(bspFile));
            }

            if (bspFile.Entities.Count == 0)
            {
                throw new ArgumentException("BSP has no entities", nameof(bspFile));
            }

            var decompiler = new TreeDecompiler(logger, bspFile, options, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            return decompiler.DecompileCore();
        }

        private MapFile DecompileCore()
        {
            var entitiesLump = _bspFile.Entities;

            Debug.Assert(entitiesLump[0].ClassName == "worldspawn");

            if (_options.MergeBrushes)
            {
                _logger.Information("Merging brushes");
            }
            else
            {
                _logger.Information("Not merging brushes");
            }

            switch (_options.BrushOptimization)
            {
                case BrushOptimization.BestTextureMatch:
                    _logger.Information("Optimizing for texture placement");
                    break;
                case BrushOptimization.FewestBrushes:
                    _logger.Information("Optimizing for fewest brushes");
                    break;
            }

            if (!_options.IncludeLiquids)
            {
                _logger.Information("Excluding brushes with liquid content types (water, slime, lava)");
            }

            MapFile mapFile = DecompilerUtils.CreateMapWithEntities(entitiesLump);

            List<DecompiledEntity> entities = new()
            {
                new DecompiledEntity(0, mapFile.Worldspawn)
            };

            entities.AddRange(mapFile.Worldspawn.Children.Cast<MapEntity>().Select((e, i) => new DecompiledEntity(i + 1, e)));

            foreach (var e in entities.Select((e, i) => new { Entity = e, Index = i }))
            {
                _cancellationToken.ThrowIfCancellationRequested();

                var entity = e.Entity;

                int? modelNumber = DecompilerUtils.TryFindAndRemoveModelNumber(_logger, entity.Entity, e.Index, _bspModels.Count);

                if (modelNumber is not null)
                {
                    CreateMapBrushes(entity, modelNumber.Value);
                }
            }

            _cancellationToken.ThrowIfCancellationRequested();

            _logger.Information("{Count} map brushes", _numMapBrushes);
            _logger.Information("{Count} clip brushes", _numClipBrushes);

            return mapFile;
        }

        private void CreateMapBrushes(DecompiledEntity entity, int modelNumber)
        {
            //create brushes from the model BSP tree
            List<BspBrush> brushlist = CreateBrushesFromBSP(modelNumber);

            //texture the brushes and split them when necesary
            brushlist = TextureBrushes(brushlist, modelNumber);

            //fix the contents textures of all brushes
            FixContentsTextures(brushlist);

            if (_options.MergeBrushes)
            {
                brushlist = MergeBrushes(brushlist, modelNumber);
            }

            _cancellationToken.ThrowIfCancellationRequested();

            if (modelNumber == 0)
            {
                _logger.Information("converting brushes to map brushes");
            }

            var origin = entity.Entity.GetOrigin();

            foreach (var brush in brushlist)
            {
                BSPBrushToMapBrush(brush, entity, origin);
                _cancellationToken.ThrowIfCancellationRequested();
            }

            if (brushlist.Count > 0 && origin != Vector3.Zero)
            {
                AddOriginBrush(entity, origin);
            }

            if (modelNumber == 0)
            {
                _logger.Information("{Count} brushes", brushlist.Count);
            }
        }

        private int FindFloatPlane(Vector3 normal, double dist)
        {
            MathUtils.SnapPlane(normal, ref dist);
            int hash = (int)Math.Abs(dist) / 8;
            hash &= (PlaneHashes - 1);

            // search the border bins as well
            for (int i = -1; i <= 1; i++)
            {
                int h = (hash + i) & (PlaneHashes - 1);
                for (var p = _planehashes[h]; p is not null; p = p.Chain)
                {
                    if (MathUtils.PlaneEqual(_bspPlanes[p.PlaneNumber], normal, dist))
                    {
                        return p.PlaneNumber;
                    }
                }
            }

            return CreateNewFloatPlane(normal, dist);
        }

        private int CreateNewFloatPlane(Vector3 normal, double dist)
        {
            if (normal.Length < 0.5)
                throw new InvalidOperationException("FloatPlane: bad normal");

            // create a new plane
            var p = new BspPlane
            {
                Normal = normal,
                Distance = dist,
                Type = MathUtils.PlaneTypeForNormal(normal)
            };

            _bspPlanes.Add(p);

            var p2 = p.ToInverted();

            _bspPlanes.Add(p2);

            // allways put axial planes facing positive first
            if (p.IsFacingNegative())
            {
                // flip order
                _bspPlanes[^1] = p;
                _bspPlanes[^2] = p2;

                AddPlaneToHash(_bspPlanes.Count - 2);
                AddPlaneToHash(_bspPlanes.Count - 1);
                return _bspPlanes.Count - 1;
            }

            AddPlaneToHash(_bspPlanes.Count - 2);
            AddPlaneToHash(_bspPlanes.Count - 1);
            return _bspPlanes.Count - 2;
        }

        private void AddPlaneToHash(int planeNumber)
        {
            var p = _bspPlanes[planeNumber];

            int hash = (int)Math.Abs(p.Distance) / 8;
            hash &= (PlaneHashes - 1);

            _planehashes[hash] = new(planeNumber, _planehashes[hash]);
        }

        /// <summary>
        /// Generates two new brushes, leaving the original unchanged
        /// TODO: merge with other version
        /// </summary>
        /// <param name="brush"></param>
        /// <param name="planenum"></param>
        /// <param name="front"></param>
        /// <param name="back"></param>
        void SplitBrush(BspBrush brush, int planenum, out BspBrush? front, out BspBrush? back)
        {
            front = back = null;
            var plane = _bspPlanes[planenum];

            // check all points
            double d_front = 0;
            double d_back = 0;

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
                    if (d > 0 && d > d_front)
                        d_front = d;
                    if (d < 0 && d < d_back)
                        d_back = d;
                }
            }

            if (d_front < 0.2) // PLANESIDE_EPSILON)
            {   // only on back
                back = new BspBrush(brush);
                return;
            }
            if (d_back > -0.2) // PLANESIDE_EPSILON)
            {   // only on front
                front = new BspBrush(brush);
                return;
            }

            // create a new winding from the split plane

            var midwinding = Winding.BaseWindingForPlane(plane.Normal, plane.Distance);

            foreach (var side in brush.Sides)
            {
                var plane2 = _bspPlanes[side.PlaneNumber ^ 1];

                midwinding = midwinding.ChopWindingInPlace(plane2.Normal, plane2.Distance, 0); // PLANESIDE_EPSILON);

                if (midwinding is null)
                {
                    break;
                }
            }

            if (midwinding is null || midwinding.IsTiny())
            {   // the brush isn't really split
                var side = MathUtils.BrushMostlyOnSide(brush, plane);

                if (side == PlaneSide.Front)
                    front = new BspBrush(brush);

                if (side == PlaneSide.Back)
                    back = new BspBrush(brush);
                return;
            }

            if (midwinding.IsHuge())
            {
                _logger.Verbose("WARNING: huge winding");
            }

            // split it for real
            var b1 = new BspBrush(brush.Sides.Count + 1);
            var b2 = new BspBrush(brush.Sides.Count + 1);

            // split all the current windings
            foreach (var side in brush.Sides)
            {
                var w = side.Winding;

                if (w is null)
                {
                    continue;
                }

                w.ClipEpsilon(plane.Normal, plane.Distance, 0 /*PLANESIDE_EPSILON*/, out var cwFront, out var cwBack);

                static void CheckClipped(BspBrush b, BspSide side, Winding? cw)
                {
                    if (cw is null)
                    {
                        return;
                    }

                    var newSide = new BspSide(side)
                    {
                        Winding = cw
                    };

                    newSide.Flags &= ~SideFlag.Tested;

                    b.Sides.Add(newSide);
                }

                CheckClipped(b1, side, cwFront);
                CheckClipped(b2, side, cwBack);
            }

            // see if we have valid polygons on both sides
            BspBrush? BoundBrushes(BspBrush b)
            {
                MathUtils.BoundBrush(b);

                bool isBogus = b.Mins.X < -MaxMapBounds || b.Maxs.X > MaxMapBounds
                    || b.Mins.Y < -MaxMapBounds || b.Maxs.Y > MaxMapBounds
                    || b.Mins.Z < -MaxMapBounds || b.Maxs.Z > MaxMapBounds;

                if (isBogus)
                {
                    _logger.Verbose("bogus brush after clip");
                }

                if (b.Sides.Count < 3 || isBogus)
                {
                    return null;
                }

                return b;
            }

            b1 = BoundBrushes(b1);
            b2 = BoundBrushes(b2);

            if (b1 is null || b2 is null)
            {
                if (b1 is null && b2 is null)
                    _logger.Verbose("split removed brush");
                else
                    _logger.Verbose("split not on both sides");

                if (b1 is not null)
                {
                    front = new BspBrush(brush);
                }

                if (b2 is not null)
                {
                    back = new BspBrush(brush);
                }

                return;
            }

            // add the midwinding to both sides
            static void AddMidWinding(BspBrush b, int i, int planenum, Winding midwinding)
            {
                var newSide = new BspSide
                {
                    PlaneNumber = planenum ^ i ^ 1,
                    //store the node number in the surf to find the texinfo later on
                    TextureInfo = TexInfoNode, //never use these sides as splitters
                    Winding = i == 0 ? midwinding.Clone() : midwinding
                };

                newSide.Flags &= ~SideFlag.Visible;
                newSide.Flags &= ~SideFlag.Tested;

                b.Sides.Add(newSide);
            }

            AddMidWinding(b1, 0, planenum, midwinding);
            AddMidWinding(b2, 1, planenum, midwinding);

            BspBrush? CheckVolume(BspBrush? b)
            {
                if (BrushVolume(b) < 1)
                {
                    //_logger.Verbose("tiny volume after clip");
                    return null;
                }

                return b;
            }

            front = CheckVolume(b1);
            back = CheckVolume(b2);

            if (front is null && back is null)
            {
                _logger.Verbose("two tiny brushes");
            }
        }

        /// <summary>
        /// Generates two new brushes, leaving the original unchanged
        /// modified for Half-Life because there are quite a lot of tiny node leaves in the Half-Life bsps
        /// </summary>
        /// <param name="brush"></param>
        /// <param name="planenum"></param>
        /// <param name="nodenum"></param>
        /// <param name="front"></param>
        /// <param name="back"></param>
        void SplitBrush(BspBrush brush, int planenum, int nodenum,
                         out BspBrush? front, out BspBrush? back)
        {
            front = back = null;
            var plane = _bspPlanes[planenum];

            // check all points
            double d_front = 0;
            double d_back = 0;

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
                    if (d > 0 && d > d_front)
                        d_front = d;
                    if (d < 0 && d < d_back)
                        d_back = d;
                }
            }

            if (d_front < 0.1) // PLANESIDE_EPSILON)
            {   // only on back
                back = new BspBrush(brush);
                _logger.Information("SplitBrush: only on back");
                return;
            }

            if (d_back > -0.1) // PLANESIDE_EPSILON)
            {   // only on front
                front = new BspBrush(brush);
                _logger.Information("SplitBrush: only on front");
                return;
            }

            // create a new winding from the split plane

            var midwinding = Winding.BaseWindingForPlane(plane.Normal, plane.Distance);

            foreach (var side in brush.Sides)
            {
                var plane2 = _bspPlanes[side.PlaneNumber ^ 1];

                midwinding = midwinding.ChopWindingInPlace(plane2.Normal, plane2.Distance, 0); // PLANESIDE_EPSILON);

                if (midwinding is null)
                {
                    break;
                }
            }

            if (midwinding is null || midwinding.IsTiny())
            {   // the brush isn't really split
                _logger.Information("SplitBrush: no split winding");
                var side = MathUtils.BrushMostlyOnSide(brush, plane);

                if (side == PlaneSide.Front)
                    front = new BspBrush(brush);

                if (side == PlaneSide.Back)
                    back = new BspBrush(brush);
                return;
            }

            if (midwinding.IsHuge())
            {
                _logger.Information("SplitBrush: WARNING huge split winding");
            }

            // split it for real
            var b1 = new BspBrush(brush.Sides.Count + 1);
            var b2 = new BspBrush(brush.Sides.Count + 1);

            // split all the current windings
            foreach (var side in brush.Sides)
            {
                var w = side.Winding;

                if (w is null)
                {
                    continue;
                }

                w.ClipEpsilon(plane.Normal, plane.Distance, 0 /*PLANESIDE_EPSILON*/, out var cwFront, out var cwBack);

                static void CheckClipped(BspBrush b, BspSide side, Winding? cw)
                {
                    if (cw is null)
                    {
                        return;
                    }

                    var newSide = new BspSide(side)
                    {
                        Winding = cw
                    };

                    newSide.Flags &= ~SideFlag.Tested;

                    b.Sides.Add(newSide);
                }

                CheckClipped(b1, side, cwFront);
                CheckClipped(b2, side, cwBack);
            }

            // see if we have valid polygons on both sides
            BspBrush? BoundBrushes(BspBrush b)
            {
                MathUtils.BoundBrush(b);

                bool isBogus = b.Mins.X < -MaxRange || b.Maxs.X > MaxRange
                    || b.Mins.Y < -MaxRange || b.Maxs.Y > MaxRange
                    || b.Mins.Z < -MaxRange || b.Maxs.Z > MaxRange;

                if (isBogus)
                {
                    _logger.Information("SplitBrush: bogus brush after clip");
                }

                if (b.Sides.Count < 3 || isBogus)
                {
                    _logger.Information("SplitBrush: numsides < 3");
                    return null;
                }

                return b;
            }

            b1 = BoundBrushes(b1);
            b2 = BoundBrushes(b2);

            if (b1 is null || b2 is null)
            {
                if (b1 is null && b2 is null)
                    _logger.Information("SplitBrush: split removed brush");
                else
                    _logger.Information("SplitBrush: split not on both sides");

                if (b1 is not null)
                {
                    front = new BspBrush(brush);
                }

                if (b2 is not null)
                {
                    back = new BspBrush(brush);
                }

                return;
            }

            // add the midwinding to both sides
            static void AddMidWinding(BspBrush b, int i, int planenum, int nodenum, Winding midwinding)
            {
                var newSide = new BspSide
                {
                    PlaneNumber = planenum ^ i ^ 1,
                    //store the node number in the surf to find the texinfo later on
                    Surface = nodenum,
                    Winding = i == 0 ? midwinding.Clone() : midwinding
                };

                newSide.Flags &= ~SideFlag.Visible;
                newSide.Flags &= ~SideFlag.Tested;

                b.Sides.Add(newSide);
            }

            AddMidWinding(b1, 0, planenum, nodenum, midwinding);
            AddMidWinding(b2, 1, planenum, nodenum, midwinding);

            BspBrush? CheckVolume(BspBrush? b)
            {
                if (BrushVolume(b) < 1)
                {
                    _logger.Information("SplitBrush: tiny volume after clip");
                    return null;
                }

                return b;
            }

            front = CheckVolume(b1);
            back = CheckVolume(b2);
        }

        private double BrushVolume(BspBrush? brush)
        {
            if (brush is null) return 0;

            // grab the first valid point as the corner
            int i;
            Winding? w = null;
            for (i = 0; i < brush.Sides.Count; ++i)
            {
                w = brush.Sides[i].Winding;
                if (w is not null) break;
            }

            if (w is null) return 0;

            var corner = w.Points[0];

            // make tetrahedrons to all other faces
            double volume = 0;
            for (; i < brush.Sides.Count; ++i)
            {
                w = brush.Sides[i].Winding;
                if (w is null) continue;
                var plane = _bspPlanes[brush.Sides[i].PlaneNumber];
                var d = -(Vector3D.Dot(corner, plane.Normal) - plane.Distance);
                var area = w.Area();
                volume += d * area;
            }

            volume /= 3;

            return volume;
        }

        private void CreateBrushWindings(BspBrush brush)
        {
            foreach (var side in brush.Sides)
            {
                var plane = _bspPlanes[side.PlaneNumber];

                var w = Winding.BaseWindingForPlane(plane.Normal, plane.Distance);

                foreach (var otherSide in brush.Sides)
                {
                    if (side == otherSide)
                    {
                        continue;
                    }

                    if ((otherSide.Flags & SideFlag.Bevel) != 0)
                    {
                        continue;
                    }

                    plane = _bspPlanes[otherSide.PlaneNumber ^ 1];

                    w = w.ChopWindingInPlace(plane.Normal, plane.Distance, 0); //CLIP_EPSILON);

                    if (w is null)
                    {
                        break;
                    }
                }

                side.Winding = w;
            }

            MathUtils.BoundBrush(brush);
        }

        private List<BspBrush> TextureBrushes(List<BspBrush> brushlist, int modelNumber)
        {
            if (modelNumber == 0) _logger.Information("texturing brushes");

            BspBrush? prevbrush = null;

            // Only check faces that are part of this model. This reduces processing time and increases accuracy.
            var model = _bspModels[modelNumber];

            //go over the brush list
            for (int brushIndex = 0; brushIndex < brushlist.Count;)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                var brush = brushlist[brushIndex];

                //find a texinfo for every brush side
                foreach (var side in brush.Sides)
                {
                    if ((side.Flags & SideFlag.Textured) != 0) continue;
                    //number of the node that created this brush side
                    int sidenodenum = side.Surface;   //see midwinding in SplitBrush

                    //no face found yet
                    int bestfacenum = -1;
                    //minimum face size
                    double largestarea = 1;
                    //if optimizing the texture placement and not going for the
                    //least number of brushes

                    if (_options.BrushOptimization == BrushOptimization.BestTextureMatch)
                    {
                        int i;
                        for (i = 0; i < model.NumFaces; ++i)
                        {
                            var faceIndex = model.FirstFace + i;
                            var face = _bspFaces[faceIndex];

                            //the face must be in the same plane as the node plane that created
                            //this brush side
                            if (face.Plane == _bspNodes[sidenodenum].Plane)
                            {
                                //get the area the face and the brush side overlap
                                var area = FaceOnWinding(face, side.Winding);
                                //if this face overlaps the brush side winding more than previous faces
                                if (area > largestarea)
                                {
                                    //if there already was a face for texturing this brush side with
                                    //a different texture
                                    if (bestfacenum >= 0 &&
                                            (_bspFaces[bestfacenum].TextureInfo != face.TextureInfo))
                                    {
                                        //split the brush to fit the texture
                                        var newbrushes = SplitBrushWithFace(brush!, face);
                                        //if new brushes where created
                                        if (newbrushes is not null)
                                        {
                                            //remove the current brush from the list
                                            brushlist.RemoveAt(brushIndex);

                                            //add the new brushes to the end of the list
                                            brushlist.AddRange(newbrushes);
                                            //don't forget about the prevbrush reference at the bottom of
                                            //the outer loop
                                            brush = prevbrush;
                                            break;
                                        }
                                        else
                                        {
                                            _logger.Verbose("brush {Count}: no real texture split", brushIndex);
                                        }
                                    }
                                    else
                                    {
                                        //best face for texturing this brush side
                                        bestfacenum = faceIndex;
                                    }
                                }
                            }
                        }
                        //if the brush was split the original brush is removed
                        //and we just continue with the next one in the list
                        if (i < model.NumFaces) break;
                    }
                    else
                    {
                        //find the face with the largest overlap with this brush side
                        //for texturing the brush side
                        for (int i = 0; i < model.NumFaces; ++i)
                        {
                            var faceIndex = model.FirstFace + i;
                            var face = _bspFaces[faceIndex];

                            //the face must be in the same plane as the node plane that created
                            //this brush side
                            if (face.Plane == _bspNodes[sidenodenum].Plane)
                            {
                                //get the area the face and the brush side overlap
                                var area = FaceOnWinding(face, side.Winding);
                                //if this face overlaps the brush side winding more than previous faces
                                if (area > largestarea)
                                {
                                    largestarea = area;
                                    bestfacenum = faceIndex;
                                }
                            }
                        }
                    }
                    //if a face was found for texturing this brush side
                    if (bestfacenum >= 0)
                    {
                        //store the texinfo number
                        side.TextureInfo = _bspFaces[bestfacenum].TextureInfo;

                        //this side is textured
                        side.Flags |= SideFlag.Textured;
                    }
                    else
                    {
                        //no texture for this side
                        side.TextureInfo = TexInfoNode;
                        //this side is textured
                        side.Flags |= SideFlag.Textured;
                    }
                }

                if (prevbrush != brush)
                {
                    ++brushIndex;
                }

                //previous brush in the list
                prevbrush = brush;
            }

            if (modelNumber == 0) _logger.Information("{Count} brushes", brushlist.Count);

            return brushlist;
        }

        /// <summary>
        /// returns the amount the face and the winding have overlap
        /// </summary>
        /// <param name="face"></param>
        /// <param name="winding"></param>
        private double FaceOnWinding(BspFace face, Winding? winding)
        {
            if (winding is null)
            {
                return 0;
            }

            var w = winding.Clone();
            var originalPlane = _bspPlanes[face.Plane];
            var plane = new BspPlane
            {
                Normal = originalPlane.Normal,
                Distance = originalPlane.Distance,
                Type = originalPlane.Type
            };

            //check on which side of the plane the face is
            if (face.Side != 0)
            {
                plane.Normal = -plane.Normal;
                plane.Distance = -plane.Distance;
            }

            for (int i = 0; i < face.NumEdges && w is not null; ++i)
            {
                //get the first and second vertex of the edge
                var edgenum = _bspSurfedges[face.FirstEdge + i];
                bool side = edgenum > 0;

                //if the face plane is flipped
                int absEdgeIndex = Math.Abs(edgenum);
                var edge = _bspEdges[absEdgeIndex];
                var v1 = _bspVertices[side ? edge.End : edge.Start];
                var v2 = _bspVertices[side ? edge.Start : edge.End];

                //create a plane through the edge vector, orthogonal to the face plane
                //and with the normal vector pointing out of the face
                var edgevec = v1 - v2;
                var normal = Vector3D.Cross(edgevec, plane.Normal);
                normal = Vector3D.Normalize(normal);
                var dist = Vector3D.Dot(normal, v1);

                w = w.ChopWindingInPlace(normal, dist, 0.9); //CLIP_EPSILON
            }

            return w?.Area() ?? 0;
        }

        /// <summary>
        /// returns a list with brushes created by splitting the given brush with planes
        /// that go through the face edges and are orthogonal to the face plane
        /// </summary>
        /// <param name="brush"></param>
        /// <param name="face"></param>
        /// <returns></returns>
        List<BspBrush>? SplitBrushWithFace(BspBrush brush, BspFace face)
        {
            var originalPlane = _bspPlanes[face.Plane];
            var plane = new BspPlane
            {
                Normal = originalPlane.Normal,
                Distance = originalPlane.Distance,
                Type = originalPlane.Type
            };

            //check on which side of the plane the face is
            if (face.Side != 0)
            {
                plane.Normal = -plane.Normal;
                plane.Distance = -plane.Distance;
            }

            List<BspBrush> brushlist = new();

            BspBrush? front = null;

            for (int i = 0; i < face.NumEdges; ++i)
            {
                //get the first and second vertex of the edge
                var edgenum = _bspSurfedges[face.FirstEdge + i];
                bool side = edgenum > 0;

                //if the face plane is flipped
                int absEdgeIndex = Math.Abs(edgenum);
                var edge = _bspEdges[absEdgeIndex];
                var v1 = _bspVertices[side ? edge.End : edge.Start];
                var v2 = _bspVertices[side ? edge.Start : edge.End];

                //create a plane through the edge vector, orthogonal to the face plane
                //and with the normal vector pointing out of the face
                var edgevec = v1 - v2;
                var normal = Vector3D.Cross(edgevec, plane.Normal);
                normal = Vector3D.Normalize(normal);
                var dist = Vector3D.Dot(normal, v1);

                int planenum = FindFloatPlane(normal, dist);
                //split the current brush
                SplitBrush(brush, planenum, out front, out var back);
                //if there is a back brush just put it in the list
                if (back is not null)
                {
                    //copy the brush contents
                    back.Side = brush.Side;

                    brushlist.Insert(0, back);
                }

                if (front is null)
                {
                    _logger.Information("SplitBrushWithFace: no new brush");
                    return null;
                }
                //copy the brush contents
                front.Side = brush.Side;
                //continue splitting the front brush
                brush = front;
            }

            if (brushlist.Count == 0)
            {
                return null;
            }

            brushlist.Insert(0, front!);

            return brushlist;
        }

        private void FixContentsTextures(List<BspBrush> brushes)
        {
            foreach (var brush in brushes)
            {
                //only fix the textures of water, slime and lava brushes
                if (brush.Side != Contents.Water &&
                    brush.Side != Contents.Slime &&
                    brush.Side != Contents.Lava)
                {
                    continue;
                }

                //if no specific contents texture was found
                var texinfonum = brush.Sides
                    .Find(s => s.TextureInfo != TexInfoNode && TextureUtils.TextureContents(_textureNameMap[s.TextureInfo]) == brush.Side)?.TextureInfo ?? -1;

                if (texinfonum == -1)
                {
                    var texInfoIndex = _textureNameMap.FirstOrDefault(t => TextureUtils.TextureContents(t.Value) == brush.Side);

                    if (texInfoIndex.Value.Length > 0)
                    {
                        texinfonum = texInfoIndex.Key;
                    }
                }

                if (texinfonum >= 0)
                {
                    //give all the brush sides this contents texture
                    foreach (var side in brush.Sides)
                    {
                        side.TextureInfo = texinfonum;
                    }
                }
                else
                {
                    _logger.Information("brush contents {Contents} with wrong textures", brush.Side);
                }
            }
        }
    }
}
