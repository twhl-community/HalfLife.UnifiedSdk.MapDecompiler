using System.Numerics;

namespace HalfLife.UnifiedSdk.MapDecompiler.TreeDecompilation
{
    internal static class Vector3Utils
    {
        public static float GetByIndex(ref Vector3 vector, int index)
        {
            return index switch
            {
                0 => vector.X,
                1 => vector.Y,
                2 => vector.Z,
                _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Vector component index out of range"),
            };
        }

        public static float GetByIndex(Vector3 vector, int index)
        {
            return GetByIndex(ref vector, index);
        }

        public static void SetByIndex(ref Vector3 vector, int index, float value)
        {
            switch (index)
            {
                case 0: vector.X = value; break;
                case 1: vector.Y = value; break;
                case 2: vector.Z = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(index), index, "Vector component index out of range");
            }
        }
    }
}
