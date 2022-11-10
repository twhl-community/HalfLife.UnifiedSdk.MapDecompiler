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
    }
}
