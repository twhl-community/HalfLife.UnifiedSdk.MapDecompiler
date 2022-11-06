using Sledge.Formats;
using Sledge.Formats.Bsp.Objects;
using System.Numerics;

namespace HalfLife.UnifiedSdk.MapDecompiler
{
    internal static class TextureUtils
    {
        public static Contents TextureContents(string name)
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
            float best = 0;
            int bestaxis = 0;

            for (int i = 0; i < 6; ++i)
            {
                var dot = Vector3.Dot(normal, baseaxis[i * 3]);
                if (dot > best)
                {
                    best = dot;
                    bestaxis = i;
                }
            }

            xv = baseaxis[bestaxis * 3 + 1];
            yv = baseaxis[bestaxis * 3 + 2];
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
            uAxis = Vector3.Normalize(Vector3.Cross(normal, tempV));
            vAxis = Vector3.Normalize(Vector3.Cross(uAxis, normal));
        }
    }
}
