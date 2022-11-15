using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace HalfLife.UnifiedSdk.MapDecompiler.GUI.Converters
{
    public sealed class JobTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.Equals(string.Empty) == false ? value : "Waiting for job to start";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return BindingNotification.UnsetValue;
        }
    }
}
