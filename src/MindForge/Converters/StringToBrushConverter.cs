using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MindForge.Converters;

/// <summary>
/// Converts a hex colour string (e.g. "#FBBF24") to a <see cref="SolidColorBrush"/>.
/// WPF's type-converter only runs at XAML parse time; data-binding requires an
/// explicit IValueConverter to perform the same string → Brush conversion at runtime.
/// </summary>
[ValueConversion(typeof(string), typeof(SolidColorBrush))]
public class StringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(color);
            }
            catch { /* fall through to default */ }
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
