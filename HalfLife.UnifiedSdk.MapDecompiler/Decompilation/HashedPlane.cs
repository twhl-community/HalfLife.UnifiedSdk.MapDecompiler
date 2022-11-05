using Sledge.Formats.Bsp.Objects;

namespace HalfLife.UnifiedSdk.MapDecompiler.Decompilation
{
    internal sealed record HashedPlane(int PlaneNumber, HashedPlane? Chain);
}
