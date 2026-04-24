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
