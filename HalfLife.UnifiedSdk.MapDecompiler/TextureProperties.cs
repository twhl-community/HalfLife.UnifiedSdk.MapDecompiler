namespace HalfLife.UnifiedSdk.MapDecompiler
{
    internal record struct TextureProperties(
        float XScale, float YScale,
        float XShift, float YShift,
        float Rotation,
        System.Numerics.Vector3 UAxis, System.Numerics.Vector3 VAxis);
}
