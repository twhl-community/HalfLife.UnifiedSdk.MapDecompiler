using Serilog;
using Sledge.Formats.Bsp;
using Sledge.Formats.Bsp.Lumps;
using Sledge.Formats.Map.Objects;
using System.Numerics;
using System.Text.RegularExpressions;
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

        private readonly Planes _bspPlanes;
        private readonly Faces _bspFaces;
        private readonly Texinfo _bspTexInfo;
        private readonly Textures _bspTextures;
        private readonly Surfedges _bspSurfedges;
        private readonly Edges _bspEdges;
        private readonly Vertices _bspVertices;
        private readonly Entities _bspEntities;
        private readonly Models _bspModels;

        private FaceToBrushDecompiler(ILogger logger, BspFile bspFile)
        {
            _logger = logger;
            _bspFile = bspFile;

            // Cache lumps to avoid lookup overhead.
            _bspPlanes = _bspFile.Planes;
            _bspFaces = _bspFile.Faces;
            _bspTexInfo = _bspFile.Texinfo;
            _bspTextures = _bspFile.Textures;
            _bspSurfedges = _bspFile.Surfedges;
            _bspEdges = _bspFile.Edges;
            _bspVertices = _bspFile.Vertices;
            _bspEntities = _bspFile.Entities;
            _bspModels = _bspFile.Models;
        }

        public static MapFile Decompile(ILogger logger, BspFile bspFile, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(bspFile);

            if (bspFile.Version != BspVersion.Goldsource)
            {
                throw new ArgumentException("BSP version not supported", nameof(bspFile));
            }

            if (bspFile.Entities.Count == 0)
            {
                throw new ArgumentException("BSP has no entities", nameof(bspFile));
            }

            var decompiler = new FaceToBrushDecompiler(logger, bspFile);

            cancellationToken.ThrowIfCancellationRequested();

            return decompiler.DecompileCore(cancellationToken);
        }

        private MapFile DecompileCore(CancellationToken cancellationToken)
        {
            MapFile mapFile = new();

            static Dictionary<string, string> CopyKeyValues(Dictionary<string, string> keyValues)
            {
                var copy = keyValues.ToDictionary(kv => kv.Key, kv => kv.Value);

                copy.Remove("classname");

                return copy;
            }

            mapFile.Worldspawn.Properties = CopyKeyValues(_bspEntities[0].KeyValues);

            mapFile.Worldspawn.Children.AddRange(_bspEntities.Skip(1).Select(e => new MapEntity
            {
                ClassName = e.ClassName,
                Properties = CopyKeyValues(e.KeyValues)
            }));

            List<MapEntity> entities = new()
            {
                mapFile.Worldspawn
            };

            entities.AddRange(mapFile.Worldspawn.Children.Cast<MapEntity>());

            cancellationToken.ThrowIfCancellationRequested();

            foreach (var entity in entities)
            {
                int modelNumber = 0;

                if (entity.ClassName != "worldspawn")
                {
                    if (!entity.Properties.TryGetValue("model", out var model) || !model.StartsWith("*"))
                    {
                        continue;
                    }

                    _ = int.TryParse(model.AsSpan()[1..], out modelNumber);

                    //don't write BSP model numbers
                    entity.Properties.Remove("model");
                }

                CreateMapBrushes(entity, modelNumber);
            }

            return mapFile;
        }

        private void CreateMapBrushes(MapEntity entity, int modelNumber)
        {
            var origin = Vector3.Zero;

            if (entity.Properties.TryGetValue("origin", out var value))
            {
                var components = Regex.Split(value, @"\s+");

                Span<float> componentValues = stackalloc float[3];

                for (int i = 0; i < 3 && i < components.Length; ++i)
                {
                    _ = float.TryParse(components[i], out componentValues[i]);
                }

                origin.X = componentValues[0];
                origin.Y = componentValues[1];
                origin.Z = componentValues[2];
            }

            var model = _bspModels[modelNumber];

            var sides = Enumerable.Range(model.FirstFace, model.NumFaces).Select(i => FaceToSide(_bspFaces[i])).ToList();

            sides = MergeSides(modelNumber, sides);

            foreach (var side in sides)
            {
                var brush = CreateMapBrush(modelNumber, side, origin);

                if (brush is not null)
                {
                    entity.Children.Add(brush);
                }
            }

            if (origin != Vector3.Zero)
            {
                entity.Children.Add(CreateOriginBrush(origin));
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

            return new(face.Plane, face.Side, face.TextureInfo, new(points));
        }

        private List<BspSide> MergeSides(int modelNumber, List<BspSide> sides)
        {
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

            for (int i = 0; i < sides.Count; ++i)
            {
                var newSide = TryMerge(side, sides[i]);

                if (newSide is null)
                {
                    continue;
                }

                // Swap out the now-merged side with the new side.
                sides[i] = newSide;
                ++mergedCount;
            }

            // didn't merge, so add at end
            if (mergedCount == 0)
            {
                sides.Add(side);
            }

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
                        if (MathF.Abs(Vector3Utils.GetByIndex(ref p1, k) - Vector3Utils.GetByIndex(ref p4, k)) > MathConstants.EqualEpsilon)
                            break;
                        if (MathF.Abs(Vector3Utils.GetByIndex(ref p2, k) - Vector3Utils.GetByIndex(ref p3, k)) > MathConstants.EqualEpsilon)
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
            //
            var plane = _bspPlanes[f1.PlaneNumber];
            var planenormal = plane.Normal;

            if (f1.Side != 0)
                planenormal = -planenormal;

            var back = f1.Winding.Points[(i + f1.Winding.Points.Count - 1) % f1.Winding.Points.Count];
            var normal = Vector3.Normalize(Vector3.Cross(planenormal, p1 - back));

            back = f2.Winding.Points[(j + 2) % f2.Winding.Points.Count];
            var dot = Vector3.Dot(back - p1, normal);

            if (dot > MathConstants.ContinuousEpsilon)
                return null;            // not a convex polygon

            var keep1 = dot < -MathConstants.ContinuousEpsilon;

            back = f1.Winding.Points[(i + 2) % f1.Winding.Points.Count];
            normal = Vector3.Normalize(Vector3.Cross(planenormal, back - p2));

            back = f2.Winding.Points[(j + f2.Winding.Points.Count - 1) % f2.Winding.Points.Count];
            dot = Vector3.Dot(back - p2, normal);

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
            if (side.Winding.Points.Count < 3)
            {
                // Should never happen since brushes are triangles at minimum.
                throw new InvalidOperationException("Face with too few edges");
            }

            const float BrushThickness = 1.0f;
            const float MinimumLength = 0.1f;

            Solid solid = new();

            var firstFrontVertex = side.Winding.Points[0];
            var secondFrontVertex = side.Winding.Points[1];
            var thirdFrontVertex = Vector3.Zero;

            var planeNormal = _bspPlanes[side.PlaneNumber].Normal;

            if (side.Side != 0)
            {
                planeNormal = -planeNormal;
            }

            var pointNormal = Vector3.Zero;

            // Find the first non-collinear point in this face.
            for (int i = 2; i < side.Winding.Points.Count; ++i)
            {
                thirdFrontVertex = side.Winding.Points[i];
                pointNormal = Vector3.Cross(Vector3.Normalize(firstFrontVertex - secondFrontVertex), Vector3.Normalize(thirdFrontVertex - secondFrontVertex));

                if (pointNormal.Length() > MinimumLength)
                {
                    break;
                }
            }

            // Some faces have only collinear points so we can't generate brushes from them.
            if (pointNormal.Length() <= MinimumLength)
            {
                _logger.Warning("Skipping model {ModelNumber} face near {FirstVertex}: face has only collinear points",
                    modelNumber, firstFrontVertex);
                return null;
            }

            var textureInfo = _bspTexInfo[side.TextureInfo];
            var texture = _bspTextures[textureInfo.MipTexture];

            var textureProperties = TextureUtils.CalculateTextureProperties(textureInfo.S, textureInfo.T, origin, planeNormal);

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

            frontFace.Vertices.Add(firstFrontVertex);
            frontFace.Vertices.Add(secondFrontVertex);
            frontFace.Vertices.Add(thirdFrontVertex);

            // Create back face from front face.
            MapFace backFace = new()
            {
                TextureName = frontFace.TextureName,
                //the scaling of the texture
                XScale = frontFace.XScale,
                YScale = frontFace.YScale,
                XShift = frontFace.XShift,
                YShift = frontFace.YShift,
                Rotation = frontFace.Rotation,
                UAxis = frontFace.UAxis,
                VAxis = frontFace.VAxis
            };

            var normal = planeNormal * -BrushThickness;

            // Add front face vertices in reverse order to flip direction and offset to create thicker brush.
            foreach (var vertex in ((IEnumerable<Vector3>)frontFace.Vertices).Reverse())
            {
                backFace.Vertices.Add(vertex + normal);
            }

            solid.Faces.Add(frontFace);
            solid.Faces.Add(backFace);

            // Generate faces for each edge to connect front and back.
            foreach (var edgeIndex in Enumerable.Range(0, side.Winding.Points.Count))
            {
                // Note: these vertices are in reversed order compared to the front faces.
                var thirdSideVertex = side.Winding.Points[edgeIndex];
                var secondSideVertex = side.Winding.Points[(edgeIndex + 1) % side.Winding.Points.Count];

                var firstSideVertex = secondSideVertex + normal;

                var sidePlaneNormal = Vector3.Cross(Vector3.Normalize(firstSideVertex - secondSideVertex), Vector3.Normalize(thirdSideVertex - secondSideVertex));

                // Align side to face.
                TextureUtils.TextureUVAxesFromNormal(sidePlaneNormal, out var uAxis, out var vAxis);

                MapFace sideFace = new()
                {
                    // TODO: add option to use NULL here.
                    TextureName = texture.Name,
                    // Use default values for most of this. No meaningful value can be assigned from the BSP data.
                    XScale = 1,
                    YScale = 1,
                    XShift = 0,
                    YShift = 0,
                    Rotation = 0,
                    UAxis = uAxis,
                    VAxis = vAxis
                };

                sideFace.Vertices.Add(firstSideVertex);
                sideFace.Vertices.Add(secondSideVertex);
                sideFace.Vertices.Add(thirdSideVertex);

                solid.Faces.Add(sideFace);
            }

            foreach (var mapFace in solid.Faces)
            {
                for (int i = 0; i < mapFace.Vertices.Count; ++i)
                {
                    mapFace.Vertices[i] += origin;
                }
            }

            return solid;
        }

        // Vertices for a face facing down the X axis, without size applied.
        private static readonly ReadOnlyMemory<Vector3> FaceVertices = new[]
        {
            new Vector3(1, 1, 1),
            new Vector3(1, 1, -1),
            new Vector3(1, -1, -1)
        };

        private const float RotationAmount = MathF.PI / 2;

        private static readonly ReadOnlyMemory<Matrix4x4> Rotations = new[]
        {
            Matrix4x4.Identity,
            Matrix4x4.CreateRotationZ(RotationAmount),
            Matrix4x4.CreateRotationY(RotationAmount),
            Matrix4x4.CreateRotationZ(RotationAmount * 2),
            Matrix4x4.CreateRotationZ(RotationAmount * 3),
            Matrix4x4.CreateRotationY(RotationAmount * 3)
        };

        private static Solid CreateOriginBrush(Vector3 origin)
        {
            //TODO: merge with constant in Tree decompiler.
            const float originBrushSize = 16;

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
                    UAxis = uAxis,
                    VAxis = vAxis
                };

                ref readonly var rotation = ref rotations[i];

                // Generate 3 vertices on this face's plane.
                foreach (ref readonly var sourceVertex in faceVertices)
                {
                    var vertex = sourceVertex * originBrushSize;

                    vertex = Vector3.Transform(vertex, rotation);

                    face.Vertices.Add(vertex + origin);
                }

                solid.Faces.Add(face);
            }

            return solid;
        }
    }
}
