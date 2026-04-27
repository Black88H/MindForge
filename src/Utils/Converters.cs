using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Microsoft.Extensions.DependencyInjection;
using MindForge.Views;

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

// Returns true when both bound values are equal — use in DataTrigger with Value="True"
// instead of the illegal Value="{Binding ...}"
public class EqualityConverter : IMultiValueConverter
{
    public static readonly EqualityConverter Instance = new();
    public object Convert(object[] values, Type t, object p, CultureInfo c)
        => values.Length == 2 && Equals(values[0], values[1]);
    public object[] ConvertBack(object value, Type[] types, object p, CultureInfo c)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a CurrentView string to the matching UserControl instance and
/// wires its DataContext from DI. Each navigation creates a fresh DI scope
/// (fresh DbContext + fresh repos + fresh ViewModel) tied to the view's
/// lifetime via Unloaded.
/// </summary>
public class ViewLocatorConverter : IValueConverter
{
    private static readonly Dictionary<string, (Type View, Type? Vm)> Map = new()
    {
        ["Dashboard"]        = (typeof(DashboardView),          typeof(MindForge.ViewModels.DashboardViewModel)),
        ["Home"]             = (typeof(HomeView),               typeof(MindForge.ViewModels.HomeViewModel)),
        ["QA"]               = (typeof(QAView),                 typeof(MindForge.ViewModels.QAViewModel)),
        ["Learning"]         = (typeof(LearningView),           typeof(MindForge.ViewModels.LearningViewModel)),
        ["Tests"]            = (typeof(TestsView),              typeof(MindForge.ViewModels.TestsViewModel)),
        ["ContentGenerator"] = (typeof(ContentGeneratorView),   typeof(MindForge.ViewModels.ContentGeneratorViewModel)),
        ["KITools"]          = (typeof(KIToolsView),            typeof(MindForge.ViewModels.KIToolsViewModel)),
        ["TestCreator"]      = (typeof(TestCreatorView),        typeof(MindForge.ViewModels.TestCreatorViewModel)),
        ["Analytics"]        = (typeof(AnalyticsView),          typeof(MindForge.ViewModels.AnalyticsViewModel)),
        ["Subjects"]         = (typeof(SubjectsView),           typeof(MindForge.ViewModels.SubjectsViewModel)),
        ["Profile"]          = (typeof(ProfileView),            typeof(MindForge.ViewModels.ProfileViewModel)),
        ["Settings"]         = (typeof(SettingsView),           typeof(MindForge.ViewModels.SettingsViewModel)),
        // Phase 1 additions
        ["Chat"]             = (typeof(ChatView),               typeof(MindForge.ViewModels.ChatViewModel)),
        ["Materialien"]      = (typeof(MaterialLibraryView),    typeof(MindForge.ViewModels.MaterialLibraryViewModel)),
        ["Wissensgraph"]     = (typeof(KnowledgeGraphView),     typeof(MindForge.ViewModels.KnowledgeGraphViewModel)),
        ["Lernplan"]         = (typeof(LearningPlanView),       typeof(MindForge.ViewModels.LearningPlanViewModel)),
    };

    public object? Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is not string key || !Map.TryGetValue(key, out var entry))
            return null;

        var view = (FrameworkElement)Activator.CreateInstance(entry.View)!;
        if (entry.Vm != null && App.Services != null)
        {
            var scope = App.Services.CreateScope();
            view.DataContext = scope.ServiceProvider.GetRequiredService(entry.Vm);
            view.Unloaded += (_, _) => scope.Dispose();
        }
        return view;
    }

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotImplementedException();
}
