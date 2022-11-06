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
        private readonly Nodes _bspNodes;
        private readonly Texinfo _bspTexInfo;
        private readonly Textures _bspTextures;
        private readonly Surfedges _bspSurfedges;
        private readonly Edges _bspEdges;
        private readonly Vertices _bspVertices;
        private readonly Leaves _bspLeaves;
        private readonly Entities _bspEntities;
        private readonly Models _bspModels;

        private FaceToBrushDecompiler(ILogger logger, BspFile bspFile)
        {
            _logger = logger;
            _bspFile = bspFile;

            // Cache lumps to avoid lookup overhead.
            _bspPlanes = _bspFile.Planes;
            _bspFaces = _bspFile.Faces;
            _bspNodes = _bspFile.Nodes;
            _bspTexInfo = _bspFile.Texinfo;
            _bspTextures = _bspFile.Textures;
            _bspSurfedges = _bspFile.Surfedges;
            _bspEdges = _bspFile.Edges;
            _bspVertices = _bspFile.Vertices;
            _bspLeaves = _bspFile.Leaves;
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

            foreach (var face in Enumerable.Range(model.FirstFace, model.NumFaces).Select(i => new { Index = i, Face = _bspFaces[i] }))
            {
                var brush = CreateMapBrush(modelNumber, face.Index, face.Face, origin);

                if (brush is not null)
                {
                    entity.Children.Add(brush);
                }
            }
        }

        private Solid? CreateMapBrush(int modelNumber, int faceIndex, BspFace face, Vector3 origin)
        {
            if (face.NumEdges < 3)
            {
                // Should never happen since brushes are triangles at minimum.
                throw new InvalidOperationException("Face with too few edges");
            }

            const float BrushThickness = 1.0f;
            const float MinimumLength = 0.1f;

            Solid solid = new();

            // Calculate plane normal (BSP planes lack planes with negative normals).
            var firstFrontVertex = GetEdgeVertices(face.FirstEdge).Start;
            var secondFrontVertex = GetEdgeVertices(face.FirstEdge + 1).Start;

            var thirdFrontVertex = Vector3.Zero;
            var planeNormal = Vector3.Zero;

            // Find the first non-collinear point in this face.
            for (int i = 2; i < face.NumEdges; ++i)
            {
                thirdFrontVertex = GetEdgeVertices(face.FirstEdge + i).Start;
                planeNormal = Vector3.Cross(Vector3.Normalize(firstFrontVertex - secondFrontVertex), Vector3.Normalize(thirdFrontVertex - secondFrontVertex));

                if (planeNormal.Length() > MinimumLength)
                {
                    break;
                }
            }

            // Some faces have only collinear points so we can't generate brushes from them.
            if (planeNormal.Length() <= MinimumLength)
            {
                _logger.Warning("Skipping model {ModelNumber} face {FaceIndex} near {FirstVertex}: face has only collinear points",
                    modelNumber, faceIndex, firstFrontVertex);
                return null;
            }

            planeNormal = Vector3.Normalize(planeNormal);

            var textureInfo = _bspTexInfo[face.TextureInfo];
            var texture = _bspTextures[textureInfo.MipTexture];

            var s = new Vector3(textureInfo.S.X, textureInfo.S.Y, textureInfo.S.Z);
            var t = new Vector3(textureInfo.T.X, textureInfo.T.Y, textureInfo.T.Z);

            float sw = textureInfo.S.W;
            float tw = textureInfo.T.W;

            TextureUtils.TextureAxisFromPlane(planeNormal, out var xAxis, out var yAxis);

            var uAxis = Vector3.Normalize(s);
            var vAxis = Vector3.Normalize(t);

            //calculate texture shift done by entity origin
            var originXShift = Vector3.Dot(origin, xAxis);
            var originYShift = Vector3.Dot(origin, yAxis);

            //the texture shift without origin shift
            var xShift = sw - originXShift;
            var yShift = tw - originYShift;

            int sv = 2;

            if (xAxis.X != 0)
            {
                sv = 0;
            }
            else if (xAxis.Y != 0)
            {
                sv = 1;
            }

            int tv = 2;

            if (yAxis.X != 0)
            {
                tv = 0;
            }
            else if (yAxis.Y != 0)
            {
                tv = 1;
            }

            //calculate rotation of texture
            float ang1 = Vector3Utils.GetByIndex(ref uAxis, tv) switch
            {
                0 => Vector3Utils.GetByIndex(ref uAxis, sv) > 0 ? 90.0f : -90.0f,
                _ => MathF.Atan2(Vector3Utils.GetByIndex(ref uAxis, sv), Vector3Utils.GetByIndex(ref uAxis, tv)) * 180 / MathF.PI
            };

            if (ang1 < 0) ang1 += 360;
            if (ang1 >= 360) ang1 -= 360;

            float ang2 = Vector3Utils.GetByIndex(ref xAxis, tv) switch
            {
                0 => Vector3Utils.GetByIndex(ref xAxis, sv) > 0 ? 90.0f : -90.0f,
                _ => MathF.Atan2(Vector3Utils.GetByIndex(ref xAxis, sv), Vector3Utils.GetByIndex(ref xAxis, tv)) * 180 / MathF.PI
            };

            if (ang2 < 0) ang2 += 360;
            if (ang2 >= 360) ang2 -= 360;

            float rotate = ang2 - ang1;

            if (rotate < 0) rotate += 360;
            if (rotate >= 360) rotate -= 360;

            // Create front face from edges.
            MapFace frontFace = new()
            {
                TextureName = texture.Name,
                //the scaling of the texture
                XScale = 1 / s.Length(),
                YScale = 1 / t.Length(),
                XShift = xShift,
                YShift = yShift,
                Rotation = rotate,
                UAxis = uAxis,
                VAxis = vAxis
            };

            frontFace.Vertices.Add(firstFrontVertex);
            frontFace.Vertices.Add(secondFrontVertex);
            frontFace.Vertices.Add(thirdFrontVertex);

            // Create back face from front face.
            MapFace backFace = new()
            {
                TextureName = texture.Name,
                //the scaling of the texture
                XScale = 1 / s.Length(),
                YScale = 1 / t.Length(),
                XShift = xShift,
                YShift = yShift,
                Rotation = rotate,
                UAxis = uAxis,
                VAxis = vAxis
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
            foreach (var edgeIndex in Enumerable.Range(face.FirstEdge, face.NumEdges))
            {
                // Note: these vertices are in reversed order compared to the front faces.
                var (thirdSideVertex, secondSideVertex) = GetEdgeVertices(edgeIndex);

                var firstSideVertex = secondSideVertex + normal;

                var sidePlaneNormal = Vector3.Cross(Vector3.Normalize(firstSideVertex - secondSideVertex), Vector3.Normalize(thirdSideVertex - secondSideVertex));

                // Align side to face.
                TextureUtils.TextureUVAxesFromNormal(sidePlaneNormal, out uAxis, out vAxis);

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

        (Vector3 Start, Vector3 End) GetEdgeVertices(int edgeIndex)
        {
            var edgenum = _bspSurfedges[edgeIndex];
            bool side = edgenum > 0;

            //if the face plane is flipped
            int absEdgeIndex = (int)MathF.Abs(edgenum);
            var edge = _bspEdges[absEdgeIndex];
            var v1 = _bspVertices[side ? edge.Start : edge.End];
            var v2 = _bspVertices[side ? edge.End : edge.Start];

            return (v1, v2);
        }
    }
}
