namespace MindForge.Services;

public interface IThemeService
{
    string CurrentTheme { get; }
    string CurrentPalette { get; }
    void ApplyTheme(string theme);
    void ApplyPalette(string palette);
    void ApplyDensity(string density);
    void ToggleTheme();
    event EventHandler<string> ThemeChanged;
}
