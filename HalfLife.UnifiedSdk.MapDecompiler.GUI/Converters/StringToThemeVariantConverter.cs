using Avalonia.Styling;
using Newtonsoft.Json;
using System;
using System.Collections.Immutable;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.Converters
{
    public sealed class StringToThemeVariantConverter : JsonConverter<ThemeVariant>
    {
        // Note: this converter assumes that the Key in each variant is a string name.
        private static readonly ImmutableArray<ThemeVariant> Variants = ImmutableArray.Create(
            ThemeVariant.Default,
            ThemeVariant.Light,
            ThemeVariant.Dark);

        public override ThemeVariant? ReadJson(JsonReader reader, Type objectType, ThemeVariant? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var name = (string?)reader.Value;

            if (name is not null)
            {
                foreach (var variant in Variants)
                {
                    if (variant.Key.ToString() == name)
                    {
                        return variant;
                    }
                }
            }

            return ThemeVariant.Default;
        }

        public override void WriteJson(JsonWriter writer, ThemeVariant? value, JsonSerializer serializer)
        {
            writer.WriteValue(value?.Key.ToString() ?? string.Empty);
        }
    }
}
