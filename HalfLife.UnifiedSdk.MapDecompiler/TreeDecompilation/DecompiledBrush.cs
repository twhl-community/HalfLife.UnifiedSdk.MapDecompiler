using System.Numerics;

namespace HalfLife.UnifiedSdk.MapDecompiler.TreeDecompilation
{
    internal sealed class DecompiledBrush
    {
        public readonly int Index;

        public Vector3 Mins;

        public Vector3 Maxs;

        public readonly BspBrush Brush;

        public DecompiledBrush(int index, BspBrush brush)
        {
            Index = index;
            Brush = brush;
        }
    }
}
