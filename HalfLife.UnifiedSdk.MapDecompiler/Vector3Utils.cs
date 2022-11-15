using Sledge.Formats.Bsp.Objects;
using System.Text.RegularExpressions;

namespace HalfLife.UnifiedSdk.MapDecompiler
{
    internal static class Vector3Utils
    {
        public static Vector3 ToDouble(this System.Numerics.Vector3 self)
        {
            return new(self.X, self.Y, self.Z);
        }

        public static System.Numerics.Vector3 ToSingle(this Vector3 self)
        {
            return new((float)self.X, (float)self.Y, (float)self.Z);
        }

        public static double GetByIndex(ref Vector3 vector, int index)
        {
            return index switch
            {
                0 => vector.X,
                1 => vector.Y,
                2 => vector.Z,
                _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Vector component index out of range"),
            };
        }

        public static double GetByIndex(Vector3 vector, int index)
        {
            return GetByIndex(ref vector, index);
        }

        public static void SetByIndex(ref Vector3 vector, int index, double value)
        {
            switch (index)
            {
                case 0: vector.X = value; break;
                case 1: vector.Y = value; break;
                case 2: vector.Z = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(index), index, "Vector component index out of range");
            }
        }

        public static Vector3 ParseVector3(string value)
        {
            var components = Regex.Split(value, @"\s+");

            Span<double> componentValues = stackalloc double[3];

            componentValues.Clear();

            for (int i = 0; i < 3 && i < components.Length; ++i)
            {
                _ = double.TryParse(components[i], out componentValues[i]);
            }

            return new(componentValues[0], componentValues[1], componentValues[2]);
        }

        /// <summary>
        ///  Given a normal, checks if it's an axial normal and if so rounds down the other components to fix existing rounding errors.
        /// </summary>
        /// <param name="normal"></param>
        public static void RoundNormal(ref Vector3 normal)
        {
            bool anyAre1 = false;

            for (int i = 0; i < 3; ++i)
            {
                if (Math.Abs(GetByIndex(ref normal, i)) == 1)
                {
                    anyAre1 = true;
                    break;
                }
            }

            if (anyAre1)
            {
                for (int i = 0; i < 3; ++i)
                {
                    SetByIndex(ref normal, i, Math.Floor(GetByIndex(ref normal, i)));
                }
            }
        }

        public static PlaneType PlaneTypeForNormal(Vector3 normal)
        {
            // NOTE: should these have an epsilon around 1.0?
            if (normal.X == 1.0 || normal.X == -1.0)
                return PlaneType.X;
            if (normal.Y == 1.0 || normal.Y == -1.0)
                return PlaneType.Y;
            if (normal.Z == 1.0 || normal.Z == -1.0)
                return PlaneType.Z;

            var ax = Math.Abs(normal.X);
            var ay = Math.Abs(normal.Y);
            var az = Math.Abs(normal.Z);

            if (ax >= ay && ax >= az)
                return PlaneType.AnyX;
            if (ay >= ax && ay >= az)
                return PlaneType.AnyY;
            return PlaneType.AnyZ;
        }
    }
}
