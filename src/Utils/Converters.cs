using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MindForge;

public class BoolToVisibilityConverter : IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();

    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        bool flag = value is true;
        if (p is string s && s == "invert") flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => value is Visibility.Visible;
}

public class BoolToWidthConverter : IValueConverter
{
    public static readonly BoolToWidthConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c) => value is true ? 56.0 : 248.0;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class PlusOneConverter : IValueConverter
{
    public static readonly PlusOneConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is int i ? (i + 1).ToString() : value;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class XPToHeightConverter : IValueConverter
{
    public static readonly XPToHeightConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is double d ? Math.Max(4, d * 120.0 / 500.0) : 4.0;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class BoolToSavingTextConverter : IValueConverter
{
    public static readonly BoolToSavingTextConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is true ? "Speichern…" : "Einstellungen speichern";
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

public class InvertBoolConverter : IValueConverter
{
    public static readonly InvertBoolConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c) => value is false;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => value is false;
}

// Visibility when int value == ConverterParameter (e.g. ConverterParameter=2)
public class IntToVisibilityConverter : IValueConverter
{
    public static readonly IntToVisibilityConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is int i && p is string s && int.TryParse(s, out int expected))
            return i == expected ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

// Onboarding wizard: visible when CurrentStep == ConverterParameter
// ConverterParameter="1+" means visible when step > 1
public class StepToVisibilityConverter : IValueConverter
{
    public static readonly StepToVisibilityConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is not int step || p is not string param) return Visibility.Collapsed;
        if (param.EndsWith("+") && int.TryParse(param.TrimEnd('+'), out int min))
            return step > min ? Visibility.Visible : Visibility.Collapsed;
        if (int.TryParse(param, out int exact))
            return step == exact ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

// Next button label for the onboarding wizard
public class StepToNextLabelConverter : IValueConverter
{
    public static readonly StepToNextLabelConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c) => value is int step
        ? step switch { 5 => "Starten →", 4 => "Weiter →", _ => "Weiter →" }
        : "Weiter →";
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

// Visible when string is non-null/non-empty; optionally matching ConverterParameter exactly
public class StringToVisibilityConverter : IValueConverter
{
    public static readonly StringToVisibilityConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var str = value as string;
        if (p is string param && !string.IsNullOrEmpty(param))
            return str == param ? Visibility.Visible : Visibility.Collapsed;
        return string.IsNullOrEmpty(str) ? Visibility.Collapsed : Visibility.Visible;
    }
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}

// True → "Ergebnis →" / False → "Weiter →" (test next button)
public class BoolToNextLabelConverter : IValueConverter
{
    public static readonly BoolToNextLabelConverter Instance = new();
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is true ? "Ergebnis →" : "Weiter →";
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
}
