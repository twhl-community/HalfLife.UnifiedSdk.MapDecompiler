using Sledge.Formats.Map.Objects;

namespace HalfLife.UnifiedSdk.MapDecompiler
{
    internal static class MapEntityExtensions
    {
        public static Vector3 GetOrigin(this Entity entity)
        {
            if (entity.Properties.TryGetValue("origin", out var value))
            {
                return Vector3Utils.ParseVector3(value);
            }

            return Vector3.Zero;
        }
    }
}
