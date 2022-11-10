using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.Converters
{
    public class DecompilerStrategyToIntConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var name = (value as string) ?? string.Empty;

            var index = DecompilerStrategies.Strategies.FindIndex(s => s.Name == name);

            if (index == -1)
            {
                index = 0;
            }

            return index;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return DecompilerStrategies.Strategies[(value as int?) ?? 0].Name;
        }
    }
}
