using Serilog;
using Sledge.Formats.Map.Objects;

namespace HalfLife.UnifiedSdk.MapDecompiler
{
    internal static class DecompilerUtils
    {
        public static int? TryFindAndRemoveModelNumber(ILogger logger, Entity entity, int entityIndex, int bspModelsCount)
        {
            int modelNumber = 0;

            if (entity.ClassName != "worldspawn")
            {
                if (!entity.Properties.TryGetValue("model", out var model) || !model.StartsWith("*"))
                {
                    return null;
                }

                _ = int.TryParse(model.AsSpan()[1..], out modelNumber);

                //don't write BSP model numbers
                entity.Properties.Remove("model");
            }

            if (modelNumber < 0 || modelNumber >= bspModelsCount)
            {
                logger.Error("Entity {Index} ({ClassName}) has invalid model index {ModelNumber} (total {ModelCount} models)",
                    entityIndex, entity.ClassName, modelNumber, bspModelsCount);
                return null;
            }

            return modelNumber;
        }
    }
}
