using System.Numerics;

namespace HalfLife.UnifiedSdk.MapDecompiler
{
    internal record struct TextureProperties(float XScale, float YScale, float XShift, float YShift, float Rotation, Vector3 UAxis, Vector3 VAxis);
}
