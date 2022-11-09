namespace HalfLife.UnifiedSdk.MapDecompiler
{
    internal static class Vector4Utils
    {
        public static Vector4 ToDouble(this System.Numerics.Vector4 self)
        {
            return new(self.X, self.Y, self.Z, self.W);
        }
    }
}
