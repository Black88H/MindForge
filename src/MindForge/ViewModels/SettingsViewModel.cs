using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindForge.Services.AI;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace MindForge.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AISelector _ai;
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MindForge", "settings.json");

    [ObservableProperty] private ObservableCollection<string> _availableModels = new();
    [ObservableProperty] private string _selectedModel = string.Empty;
    [ObservableProperty] private string _ollamaUrl = "http://localhost:11434";
    [ObservableProperty] private string _ollamaStatus = string.Empty;
    [ObservableProperty] private bool _isLoadingModels;
    [ObservableProperty] private string _preferredSummarizationModel = string.Empty;
    [ObservableProperty] private string _preferredChatModel = string.Empty;

    public SettingsViewModel(AISelector ai) => _ai = ai;

    public void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(SettingsPath));
            if (doc.RootElement.TryGetProperty("ollamaUrl", out var u))
                OllamaUrl = u.GetString() ?? OllamaUrl;
            if (doc.RootElement.TryGetProperty("preferredChatModel", out var cm))
                PreferredChatModel = cm.GetString() ?? string.Empty;
            if (doc.RootElement.TryGetProperty("preferredSummarizationModel", out var sm))
                PreferredSummarizationModel = sm.GetString() ?? string.Empty;
        }
        catch { /* ignore */ }
    }

    [RelayCommand]
    public async Task RefreshModelsAsync()
    {
        IsLoadingModels = true;
        OllamaStatus = "⏳ Connecting...";
        _ai.SetOllamaUrl(OllamaUrl);

        try
        {
            var models = await _ai.GetAvailableModelsAsync();
            AvailableMaterials(models);

            OllamaStatus = models.Count > 0
                ? $"✅ {models.Count} model(s) available"
                : "⚠️ Connected but no models installed";
        }
        catch (Exception ex)
        {
            OllamaStatus = $"❌ {ex.Message}";
        }
        finally { IsLoadingModels = false; }
    }

    private void AvailableMaterials(System.Collections.Generic.List<string> models)
    {
        AvailableModels = new ObservableCollection<string>(models);
        if (AvailableModels.Count > 0 && string.IsNullOrEmpty(PreferredChatModel))
            PreferredChatModel = AvailableModels[0];
    }

    [RelayCommand]
    public void SaveModelSettings()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);

            // Merge with existing settings
            var existing = new System.Collections.Generic.Dictionary<string, object?>();
            if (File.Exists(SettingsPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(SettingsPath));
                foreach (var prop in doc.RootElement.EnumerateObject())
                    existing[prop.Name] = prop.Value.GetRawText();
            }

            existing["ollamaUrl"] = $"\"{OllamaUrl}\"";
            existing["preferredChatModel"] = $"\"{PreferredChatModel}\"";
            existing["preferredSummarizationModel"] = $"\"{PreferredSummarizationModel}\"";

            _ai.SetOllamaUrl(OllamaUrl);

            // Write simple JSON manually to avoid raw-JSON nesting issues
            var json = new System.Text.StringBuilder("{");
            bool first = true;
            foreach (var kv in existing)
            {
                if (!first) json.Append(',');
                json.Append($"\"{kv.Key}\":{kv.Value}");
                first = false;
            }
            json.Append('}');

            var opts = new JsonSerializerOptions { WriteIndented = true };
            using var parsed = JsonDocument.Parse(json.ToString());
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(parsed, opts));

            OllamaStatus = "✅ Settings saved.";
        }
        catch (Exception ex)
        {
            OllamaStatus = $"❌ Save failed: {ex.Message}";
        }
    }
}
