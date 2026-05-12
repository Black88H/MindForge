using System;
using System.Globalization;
using System.Windows.Data;
using MindForge.Models;

namespace MindForge.Converters;

/// <summary>
/// Converts an <see cref="AnnotationType"/> enum value to its German display label.
/// </summary>
[ValueConversion(typeof(AnnotationType), typeof(string))]
public class AnnotationTypeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is AnnotationType t ? t switch
        {
            AnnotationType.Highlight => "Markierung",
            AnnotationType.Important => "Wichtig",
            AnnotationType.Question  => "Frage",
            AnnotationType.Concept   => "Konzept",
            AnnotationType.Example   => "Beispiel",
            AnnotationType.Todo      => "To-Do",
            AnnotationType.Confusion => "Verwirrend",
            _                        => value.ToString() ?? ""
        } : (value?.ToString() ?? "");

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
