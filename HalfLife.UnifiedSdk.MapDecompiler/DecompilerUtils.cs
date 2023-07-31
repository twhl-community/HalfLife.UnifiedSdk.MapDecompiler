using Serilog;
using Sledge.Formats.Bsp.Lumps;
using Sledge.Formats.Map.Objects;

namespace HalfLife.UnifiedSdk.MapDecompiler
{
    internal static class DecompilerUtils
    {
        public static void PrintSharedOptions(ILogger logger, DecompilerOptions options)
        {
            if (options.ApplyNullToGeneratedFaces)
            {
                logger.Information("Applying NULL to generated faces");
            }
        }

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
            else if (entityIndex != 0)
            {
                logger.Warning("Entity {Index} has class worldspawn which is not allowed (entity 0 must be the only worldspawn entity), ignoring",
                    entityIndex);
                return null;
            }

            if (modelNumber < 0 || modelNumber >= bspModelsCount)
            {
                logger.Warning("Entity {Index} ({ClassName}) has invalid model index {ModelNumber} (total {ModelCount} models)",
                    entityIndex, entity.ClassName, modelNumber, bspModelsCount);
                return null;
            }

            return modelNumber;
        }

        public static MapFile CreateMapWithEntities(Entities entities)
        {
            MapFile mapFile = new();

            static void CopyKeyValues(Entity entity, List<KeyValuePair<string, string>> keyValues)
            {
                var list = entity.SortedProperties;

                list.Clear();
                list.AddRange(keyValues);
                list.RemoveAll(kv => kv.Key == "classname");
            }

             CopyKeyValues(mapFile.Worldspawn, entities[0].SortedKeyValues);

            mapFile.Worldspawn.Children.AddRange(entities.Skip(1).Select(e =>
            {
                var entity = new Entity
                {
                    ClassName = e.ClassName
                };

                CopyKeyValues(entity, e.SortedKeyValues);

                return entity;
            }));

            return mapFile;
        }
    }
}
