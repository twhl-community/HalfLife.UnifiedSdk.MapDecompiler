﻿using Sledge.Formats.Bsp.Objects;

namespace HalfLife.UnifiedSdk.MapDecompiler
{
    internal static class TextureUtils
    {
        public static Contents TextureContents(string name)
        {
            if (name.Length > 0)
            {
                if (name.StartsWith("sky", StringComparison.OrdinalIgnoreCase))
                    return Contents.Solid;

                if (name.AsSpan()[1..].StartsWith("!lava", StringComparison.OrdinalIgnoreCase))
                    return Contents.Lava;

                if (name.AsSpan()[1..].StartsWith("!slime", StringComparison.OrdinalIgnoreCase))
                    return Contents.Slime;

                /*
                if (!Q_strncasecmp (name, "!cur_90",7))
                    return CONTENTS_CURRENT_90;
                if (!Q_strncasecmp (name, "!cur_0",6))
                    return CONTENTS_CURRENT_0;
                if (!Q_strncasecmp (name, "!cur_270",8))
                    return CONTENTS_CURRENT_270;
                if (!Q_strncasecmp (name, "!cur_180",8))
                    return CONTENTS_CURRENT_180;
                if (!Q_strncasecmp (name, "!cur_up",7))
                    return CONTENTS_CURRENT_UP;
                if (!Q_strncasecmp (name, "!cur_dwn",8))
                    return CONTENTS_CURRENT_DOWN;
                //*/
                if (name.StartsWith("!"))
                    return Contents.Water;
                /*
                if (!Q_strncasecmp (name, "origin",6))
                    return CONTENTS_ORIGIN;
                if (!Q_strncasecmp (name, "clip",4))
                    return CONTENTS_CLIP;
                if( !Q_strncasecmp( name, "translucent", 11 ) )
                    return CONTENTS_TRANSLUCENT;
                if( name[0] == '@' )
                    return CONTENTS_TRANSLUCENT;
                //*/
            }

            return Contents.Solid;
        }

        private static readonly Vector3[] baseaxis = new[]
        {
            new Vector3(0, 0, 1), new Vector3(1, 0, 0), new Vector3(0, -1, 0),		// floor
            new Vector3(0, 0, -1), new Vector3(1, 0, 0), new Vector3(0, -1, 0),		// ceiling
            new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, -1),		// west wall
            new Vector3(-1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, -1),		// east wall
            new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, -1),		// south wall
            new Vector3(0, -1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, -1)		// north wall
        };

        public static void TextureAxisFromPlane(Vector3 normal, out Vector3 xv, out Vector3 yv)
        {
            double best = 0;
            int bestaxis = 0;

            for (int i = 0; i < 6; ++i)
            {
                var dot = Vector3D.Dot(normal, baseaxis[i * 3]);
                if (dot > best)
                {
                    best = dot;
                    bestaxis = i;
                }
            }

            xv = baseaxis[(bestaxis * 3) + 1];
            yv = baseaxis[(bestaxis * 3) + 2];
        }

        /// <summary>
        /// Based on https://github.com/LogicAndTrick/sledge-formats/blob/b9b5f956a694a5faa9a307afb364c090573c2d05/Sledge.Formats/NumericsExtensions.cs#L52-L60
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static Vector3 ClosestAxis(this Vector3 self)
        {
            // VHE prioritises the axes in order of X, Y, Z.
            var norm = Vector3D.Abs(self);

            if (norm.X >= norm.Y && norm.X >= norm.Z) return Vector3.UnitX;
            if (norm.Y >= norm.Z) return Vector3.UnitY;
            return Vector3.UnitZ;
        }

        /// <summary>
        /// Based on https://github.com/LogicAndTrick/sledge/blob/a2ea69dfbd72350bc298d589d7645a647afd4303/Sledge.BspEditor/Primitives/TextureExtensions.cs#L13-L22
        /// </summary>
        /// <param name="normal"></param>
        /// <param name="uAxis"></param>
        /// <param name="vAxis"></param>
        public static void TextureUVAxesFromNormal(Vector3 normal, out Vector3 uAxis, out Vector3 vAxis)
        {
            var closestAxis = normal.ClosestAxis();

            var tempV = closestAxis == Vector3.UnitZ ? -Vector3.UnitY : -Vector3.UnitZ;
            uAxis = Vector3D.Normalize(Vector3D.Cross(normal, tempV));
            vAxis = Vector3D.Normalize(Vector3D.Cross(uAxis, normal));
        }

        public static TextureProperties CalculateTextureProperties(Vector4 s, Vector4 t, Vector3 origin, Vector3 planeNormal)
        {
            var s3 = new Vector3(s.X, s.Y, s.Z);
            var t3 = new Vector3(t.X, t.Y, t.Z);

            var sw = s.W;
            var tw = t.W;

            TextureAxisFromPlane(planeNormal, out var xAxis, out var yAxis);

            var uAxis = Vector3D.Normalize(s3);
            var vAxis = Vector3D.Normalize(t3);

            var textureAxis = Vector3D.Cross(uAxis, vAxis);

            if (Math.Abs(Vector3D.Dot(planeNormal, textureAxis)) < 0.01)
            {
                // Texture axis is perpendicular to plane. Use face-aligned axis.
                TextureUVAxesFromNormal(planeNormal, out uAxis, out vAxis);
            }

            //calculate texture shift done by entity origin
            var originXShift = Vector3D.Dot(origin, s3);
            var originYShift = Vector3D.Dot(origin, t3);

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
            var ang1 = Vector3Utils.GetByIndex(ref uAxis, tv) switch
            {
                0 => Vector3Utils.GetByIndex(ref uAxis, sv) > 0 ? 90.0 : -90.0,
                _ => Math.Atan2(Vector3Utils.GetByIndex(ref uAxis, sv), Vector3Utils.GetByIndex(ref uAxis, tv)) * 180 / Math.PI
            };

            if (ang1 < 0) ang1 += 360;
            if (ang1 >= 360) ang1 -= 360;

            var ang2 = Vector3Utils.GetByIndex(ref xAxis, tv) switch
            {
                0 => Vector3Utils.GetByIndex(ref xAxis, sv) > 0 ? 90.0 : -90.0,
                _ => Math.Atan2(Vector3Utils.GetByIndex(ref xAxis, sv), Vector3Utils.GetByIndex(ref xAxis, tv)) * 180 / Math.PI
            };

            if (ang2 < 0) ang2 += 360;
            if (ang2 >= 360) ang2 -= 360;

            var rotate = ang2 - ang1;

            if (rotate < 0) rotate += 360;
            if (rotate >= 360) rotate -= 360;

            return new TextureProperties(
                XScale: (float)(1 / s3.Length),
                YScale: (float)(1 / t3.Length),
                XShift: (float)xShift,
                YShift: (float)yShift,
                Rotation: (float)rotate,
                UAxis: uAxis.ToSingle(),
                VAxis: vAxis.ToSingle());
        }
    }
}
