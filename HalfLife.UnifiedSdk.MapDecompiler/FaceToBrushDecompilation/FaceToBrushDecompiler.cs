using Serilog;
using Sledge.Formats.Bsp;
using Sledge.Formats.Bsp.Lumps;
using Sledge.Formats.Map.Objects;
using BspFace = Sledge.Formats.Bsp.Objects.Face;
using BspVersion = Sledge.Formats.Bsp.Version;
using MapEntity = Sledge.Formats.Map.Objects.Entity;
using MapFace = Sledge.Formats.Map.Objects.Face;

namespace HalfLife.UnifiedSdk.MapDecompiler.FaceToBrushDecompilation
{
    /// <summary>
    /// Decompiles each face to its own brush.
    /// </summary>
    internal sealed class FaceToBrushDecompiler
    {
        private readonly ILogger _logger;
        private readonly BspFile _bspFile;
        private readonly DecompilerOptions _options;

        private readonly List<BspPlane> _bspPlanes;
        private readonly Faces _bspFaces;
        private readonly Texinfo _bspTexInfo;
        private readonly Textures _bspTextures;
        private readonly Surfedges _bspSurfedges;
        private readonly Edges _bspEdges;
        private readonly List<Vector3> _bspVertices;
        private readonly Models _bspModels;

        private FaceToBrushDecompiler(ILogger logger, BspFile bspFile, DecompilerOptions options)
        {
            _logger = logger;
            _bspFile = bspFile;
            _options = options;

            // Cache lumps to avoid lookup overhead.
            _bspPlanes = _bspFile.Planes.Select(p => new BspPlane(p)).ToList();
            _bspFaces = _bspFile.Faces;
            _bspTexInfo = _bspFile.Texinfo;
            _bspTextures = _bspFile.Textures;
            _bspSurfedges = _bspFile.Surfedges;
            _bspEdges = _bspFile.Edges;
            _bspVertices = _bspFile.Vertices.Select(v => v.ToDouble()).ToList();
            _bspModels = _bspFile.Models;
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

            var decompiler = new FaceToBrushDecompiler(logger, bspFile, options);

            cancellationToken.ThrowIfCancellationRequested();

            return decompiler.DecompileCore(cancellationToken);
        }

        private MapFile DecompileCore(CancellationToken cancellationToken)
        {
            DecompilerUtils.PrintSharedOptions(_logger, _options);

            MapFile mapFile = DecompilerUtils.CreateMapWithEntities(_bspFile.Entities);

            List<MapEntity> entities = new()
            {
                mapFile.Worldspawn
            };

            entities.AddRange(mapFile.Worldspawn.Children.Cast<MapEntity>());

            foreach (var e in entities.Select((e, i) => new { Entity = e, Index = i }))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entity = e.Entity;

                int? modelNumber = DecompilerUtils.TryFindAndRemoveModelNumber(_logger, entity, e.Index, _bspModels.Count);

                if (modelNumber is not null)
                {
                    CreateMapBrushes(entity, modelNumber.Value);
                }
            }

            return mapFile;
        }

        private void CreateMapBrushes(MapEntity entity, int modelNumber)
        {
            var origin = entity.GetOrigin();

            var model = _bspModels[modelNumber];

            var sides = Enumerable.Range(model.FirstFace, model.NumFaces).Select(i => FaceToSide(_bspFaces[i])).ToList();

            RemoveBadSides(modelNumber, sides);

            sides = MergeSides(modelNumber, sides);

            int brushCount = 0;

            foreach (var side in sides)
            {
                var brush = CreateMapBrush(modelNumber, side, origin);

                if (brush is not null)
                {
                    entity.Children.Add(brush);
                    ++brushCount;
                }
            }

            if (_options.AlwaysGenerateOriginBrushes || origin != Vector3.Zero)
            {
                entity.Children.Add(CreateOriginBrush(origin));
                ++brushCount;
            }

            if (modelNumber == 0)
            {
                _logger.Information("{Count} brushes", brushCount);
            }
        }

        private BspSide FaceToSide(BspFace face)
        {
            List<Vector3> points = new(face.NumEdges);

            for (var i = 0; i < face.NumEdges; ++i)
            {
                var edgenum = _bspSurfedges[face.FirstEdge + i];
                bool side = edgenum > 0;

                //if the face plane is flipped
                int absEdgeIndex = Math.Abs(edgenum);
                var edge = _bspEdges[absEdgeIndex];
                points.Add(_bspVertices[side ? edge.Start : edge.End]);
            }

            // Remove collinear points now so the merging algorithm doesn't accidentally create concave windings.
            Winding winding = new(Winding.RemoveCollinearPoints(points));

            return new(face.Plane, face.Side, face.TextureInfo, winding);
        }

        private void RemoveBadSides(int modelNumber, List<BspSide> sides)
        {
            int absoluteIndex = 0;

            for (int i = 0; i < sides.Count;)
            {
                var side = sides[i];

                if (side.Winding.Points.Count < 3)
                {
                    _logger.Warning("Skipping model {ModelNumber} face {Index}: face has only collinear points",
                            modelNumber, absoluteIndex);
                    
                    sides.RemoveAt(i);
                }
                else
                {
                    ++i;
                }

                ++absoluteIndex;
            }
        }

        private List<BspSide> MergeSides(int modelNumber, List<BspSide> sides)
        {
            if (modelNumber == 0)
            {
                _logger.Information("Merging faces");
            }

            List<BspSide> result = new(sides.Count);

            int totalMergedCount = 0;
            int mergedCount;

            // Keep trying to merge until a pass doesn't merge anything.
            // This way all possible merges are performed.
            do
            {
                mergedCount = 0;

                foreach (var side in sides)
                {
                    mergedCount += MergeSideToList(side, result);
                }

                totalMergedCount += mergedCount;

                if (mergedCount > 0)
                {
                    sides = result;
                    result = new(sides.Count);
                }
            }
            while (mergedCount > 0);

            if (totalMergedCount > 0)
            {
                _logger.Information("Model {ModelNumber}: merged {Count} faces", modelNumber, totalMergedCount);
            }

            return result;
        }

        private int MergeSideToList(BspSide side, List<BspSide> sides)
        {
            int mergedCount = 0;

            for (int i = 0; i < sides.Count;)
            {
                var newSide = TryMerge(side, sides[i]);

                if (newSide is null)
                {
                    ++i;
                    continue;
                }

                // Replace original with merged side and remove other side from list.
                sides.RemoveAt(i);
                side = newSide;
                ++mergedCount;
            }

            // Add original or merged side.
            sides.Add(side);

            return mergedCount;
        }

        /// <summary>
        /// If two polygons share a common edge and the edges that meet at the
        /// common points are both inside the other polygons, merge them
        /// </summary>
        /// <param name="f1"></param>
        /// <param name="f2"></param>
        private BspSide? TryMerge(BspSide f1, BspSide f2)
        {
            if (f1.PlaneNumber != f2.PlaneNumber)
                return null;

            if (f1.Side != f2.Side)
                return null;

            if (f1.TextureInfo != f2.TextureInfo)
                return null;

            //
            // find a common edge
            //	
            var p1 = Vector3.Zero;
            var p2 = Vector3.Zero;

            int i;
            int j = 0;

            for (i = 0; i < f1.Winding.Points.Count; ++i)
            {
                p1 = f1.Winding.Points[i];
                p2 = f1.Winding.Points[(i + 1) % f1.Winding.Points.Count];

                for (j = 0; j < f2.Winding.Points.Count; ++j)
                {
                    var p3 = f2.Winding.Points[j];
                    var p4 = f2.Winding.Points[(j + 1) % f2.Winding.Points.Count];

                    int k;
                    for (k = 0; k < 3; ++k)
                    {
                        if (Math.Abs(Vector3Utils.GetByIndex(ref p1, k) - Vector3Utils.GetByIndex(ref p4, k)) > MathConstants.EqualEpsilon)
                            break;
                        if (Math.Abs(Vector3Utils.GetByIndex(ref p2, k) - Vector3Utils.GetByIndex(ref p3, k)) > MathConstants.EqualEpsilon)
                            break;
                    }

                    if (k == 3)
                        break;
                }

                if (j < f2.Winding.Points.Count)
                    break;
            }

            if (i == f1.Winding.Points.Count)
                return null;            // no matching edges

            //
            // check slope of connected lines
            // if the slopes are colinear, the point can be removed
            // Note that this assumes the polygons don't have collinear points.
            //
            var plane = _bspPlanes[f1.PlaneNumber];
            var planenormal = plane.Normal;

            if (f1.Side != 0)
                planenormal = -planenormal;

            var back = f1.Winding.Points[(i + f1.Winding.Points.Count - 1) % f1.Winding.Points.Count];
            var normal = Vector3D.Normalize(Vector3D.Cross(planenormal, p1 - back));

            back = f2.Winding.Points[(j + 2) % f2.Winding.Points.Count];
            var dot = Vector3D.Dot(back - p1, normal);

            if (dot > MathConstants.ContinuousEpsilon)
                return null;            // not a convex polygon

            var keep1 = dot < -MathConstants.ContinuousEpsilon;

            back = f1.Winding.Points[(i + 2) % f1.Winding.Points.Count];
            normal = Vector3D.Normalize(Vector3D.Cross(planenormal, back - p2));

            back = f2.Winding.Points[(j + f2.Winding.Points.Count - 1) % f2.Winding.Points.Count];
            dot = Vector3D.Dot(back - p2, normal);

            if (dot > MathConstants.ContinuousEpsilon)
                return null;            // not a convex polygon

            var keep2 = dot < -MathConstants.ContinuousEpsilon;

            //
            // build the new polygon
            //

            BspSide newf = new(f1.PlaneNumber, f1.Side, f1.TextureInfo, new(f1.Winding.Points.Count + f2.Winding.Points.Count));

            // copy first polygon
            for (int k = (i + 1) % f1.Winding.Points.Count; k != i; k = (k + 1) % f1.Winding.Points.Count)
            {
                if (k == (i + 1) % f1.Winding.Points.Count && !keep2)
                    continue;

                newf.Winding.Points.Add(f1.Winding.Points[k]);
            }

            // copy second polygon
            for (int l = (j + 1) % f2.Winding.Points.Count; l != j; l = (l + 1) % f2.Winding.Points.Count)
            {
                if (l == (j + 1) % f2.Winding.Points.Count && !keep1)
                    continue;

                newf.Winding.Points.Add(f2.Winding.Points[l]);
            }

            return newf;
        }

        private Solid? CreateMapBrush(int modelNumber, BspSide side, Vector3 origin)
        {
            var winding = side.Winding;

            var planeNormal = _bspPlanes[side.PlaneNumber].Normal;

            if (side.Side != 0)
            {
                planeNormal = -planeNormal;
            }

            if (winding.IsTiny())
            {
                _logger.Warning("Skipping model {ModelNumber} face near {FirstVertex}: face is tiny",
                        modelNumber, winding.Points[0]);
                return null;
            }

            if (winding.IsHuge())
            {
                _logger.Warning("Skipping model {ModelNumber} face near {FirstVertex}: face is huge",
                        modelNumber, winding.Points[0]);
                return null;
            }

            Solid solid = new();

            var textureInfo = _bspTexInfo[side.TextureInfo];
            var texture = _bspTextures[textureInfo.MipTexture];

            var textureProperties = TextureUtils.CalculateTextureProperties(textureInfo.S.ToDouble(), textureInfo.T.ToDouble(), origin, planeNormal);

            MapFace frontFace = new()
            {
                TextureName = texture.Name,
                //the scaling of the texture
                XScale = textureProperties.XScale,
                YScale = textureProperties.YScale,
                XShift = textureProperties.XShift,
                YShift = textureProperties.YShift,
                Rotation = textureProperties.Rotation,
                UAxis = textureProperties.UAxis,
                VAxis = textureProperties.VAxis
            };

            var frontVertices = new[]
            {
                winding.Points[2],
                winding.Points[1],
                winding.Points[0]
            };

            frontFace.Vertices.AddRange(frontVertices.Select(v => (v + origin).ToSingle()));

            // Create back face from front face.
            MapFace backFace = new()
            {
                TextureName = _options.ApplyNullToGeneratedFaces ? "NULL" : frontFace.TextureName,
                //the scaling of the texture
                XScale = frontFace.XScale,
                YScale = frontFace.YScale,
                XShift = frontFace.XShift,
                YShift = frontFace.YShift,
                Rotation = frontFace.Rotation,
                UAxis = frontFace.UAxis,
                VAxis = frontFace.VAxis
            };

            var normal = planeNormal * -MapDecompilerConstants.FaceToBrushThickness;

            // Add front face vertices in reverse order to flip direction and offset to create thicker brush.
            backFace.Vertices.AddRange(frontVertices.Reverse().Select(v => (v + origin + normal).ToSingle()));

            solid.Faces.Add(frontFace);
            solid.Faces.Add(backFace);

            // Generate faces for each edge to connect front and back.
            foreach (var edgeIndex in Enumerable.Range(0, winding.Points.Count))
            {
                // Note: these vertices are in reversed order compared to the front faces.
                var thirdSideVertex = winding.Points[edgeIndex];
                var secondSideVertex = winding.Points[(edgeIndex + 1) % winding.Points.Count];

                var firstSideVertex = secondSideVertex + normal;

                var sidePlaneNormal = Vector3D.Cross(Vector3D.Normalize(firstSideVertex - secondSideVertex), Vector3D.Normalize(thirdSideVertex - secondSideVertex));

                // Align side to face.
                TextureUtils.TextureUVAxesFromNormal(sidePlaneNormal, out var uAxis, out var vAxis);

                MapFace sideFace = new()
                {
                    TextureName = _options.ApplyNullToGeneratedFaces ? "NULL" : texture.Name,
                    // Use default values for most of this. No meaningful value can be assigned from the BSP data.
                    XScale = 1,
                    YScale = 1,
                    XShift = 0,
                    YShift = 0,
                    Rotation = 0,
                    UAxis = uAxis.ToSingle(),
                    VAxis = vAxis.ToSingle()
                };

                sideFace.Vertices.Add((thirdSideVertex + origin).ToSingle());
                sideFace.Vertices.Add((secondSideVertex + origin).ToSingle());
                sideFace.Vertices.Add((firstSideVertex + origin).ToSingle());

                solid.Faces.Add(sideFace);
            }

            return solid;
        }

        // Vertices for a face facing down the X axis, without size applied.
        private static readonly ReadOnlyMemory<Vector3> FaceVertices = new[]
        {
            new Vector3(1, -1, -1),
            new Vector3(1, 1, -1),
            new Vector3(1, 1, 1)
        };

        private const double RotationAmount = Math.PI / 2;

        private static readonly ReadOnlyMemory<Matrix4x4> Rotations = new[]
        {
            Matrix4x4.Identity,
            Matrix4x4D.CreateRotationZ(RotationAmount),
            Matrix4x4D.CreateRotationY(RotationAmount),
            Matrix4x4D.CreateRotationZ(RotationAmount * 2),
            Matrix4x4D.CreateRotationZ(RotationAmount * 3),
            Matrix4x4D.CreateRotationY(RotationAmount * 3)
        };

        private static Solid CreateOriginBrush(Vector3 origin)
        {
            Solid solid = new();

            // Create six faces, each facing along a major axis.
            var faceVertices = FaceVertices.Span;
            var rotations = Rotations.Span;

            for (int i = 0; i < 6; ++i)
            {
                var normal = Vector3.Zero;

                Vector3Utils.SetByIndex(ref normal, i % 3, i >= 3 ? -1 : 1);

                TextureUtils.TextureUVAxesFromNormal(normal, out var uAxis, out var vAxis);

                MapFace face = new()
                {
                    TextureName = "ORIGIN",
                    XScale = 1,
                    YScale = 1,
                    UAxis = uAxis.ToSingle(),
                    VAxis = vAxis.ToSingle()
                };

                ref readonly var rotation = ref rotations[i];

                // Generate 3 vertices on this face's plane.
                foreach (ref readonly var sourceVertex in faceVertices)
                {
                    var vertex = sourceVertex * MapDecompilerConstants.OriginBrushHalfSize;

                    vertex = Vector3D.Transform(vertex, rotation);

                    face.Vertices.Add((vertex + origin).ToSingle());
                }

                solid.Faces.Add(face);
            }

            return solid;
        }
    }
}
