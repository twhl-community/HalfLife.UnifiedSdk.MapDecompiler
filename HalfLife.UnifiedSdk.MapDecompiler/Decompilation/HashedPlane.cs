using Sledge.Formats.Bsp.Objects;

namespace HalfLife.UnifiedSdk.MapDecompiler.Decompilation
{
    internal sealed record HashedPlane(Plane Plane, HashedPlane? Chain);
}
