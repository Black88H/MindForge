using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindForge.Models;
using MindForge.Services;

namespace MindForge.ViewModels;

public partial class SubjectsViewModel : ObservableObject
{
    private readonly SubjectRepository _repo;

    public SubjectsViewModel(SubjectRepository repo)
    {
        _repo = repo;
        _ = LoadAsync();
    }

    [ObservableProperty] private ObservableCollection<SubjectViewModel> _subjects = new();

    [ObservableProperty] private SubjectViewModel? _selectedSubject;
    [ObservableProperty] private bool _showAddDialog = false;
    [ObservableProperty] private bool _showEditDialog = false;
    [ObservableProperty] private bool _isLoading = false;

    public bool IsDialogOpen => ShowAddDialog || ShowEditDialog;
    public string DialogTitle => ShowAddDialog ? "Fach hinzufügen" : "Fach bearbeiten";
    public bool IsEmpty => !IsLoading && Subjects.Count == 0;

    partial void OnShowAddDialogChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDialogOpen));
        OnPropertyChanged(nameof(DialogTitle));
    }
    partial void OnShowEditDialogChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDialogOpen));
        OnPropertyChanged(nameof(DialogTitle));
    }
    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    // Edit form
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private string _newIcon = "∫";
    [ObservableProperty] private string _newColor = "#5B8CFF";
    [ObservableProperty] private string _newDifficulty = "Mittel";
    [ObservableProperty] private string _newDescription = string.Empty;

    public string EditName { get => NewName; set => NewName = value; }
    partial void OnNewNameChanged(string value) => OnPropertyChanged(nameof(EditName));

    public ObservableCollection<SelectableItem> AvailableIcons { get; } = new(
        new[] { "∫", "ψ", "En", "⌬", "{ }", "◐", "∑", "π", "∞", "α", "🧬", "📈", "⚛", "🔬" }
        .Select(i => new SelectableItem { Value = i }));

    public ObservableCollection<SelectableItem> AvailableColors { get; } = new(
        new[] { "#5B8CFF", "#BD93F9", "#3fcf8e", "#ffb547", "#ff6b9d", "#5eead4", "#4CAF50", "#FF6B35" }
        .Select(c => new SelectableItem { Value = c }));

    public List<string> PresetColors { get; } = ["#5B8CFF","#BD93F9","#3fcf8e","#ffb547","#ff6b9d","#5eead4","#4CAF50","#FF6B35"];
    public List<string> PresetIcons  { get; } = ["∫","ψ","En","⌬","{ }","◐","∑","π","∞","α","🧬","📈","⚛","🔬"];
    public List<string> Difficulties { get; } = ["Leicht","Mittel","Schwer"];

    public string TotalStats => $"{Subjects.Count} Fächer · {Subjects.Sum(s => s.QuestionCount)} Fragen";

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var subjects = await _repo.GetSubjectsAsync();
            Subjects = new ObservableCollection<SubjectViewModel>(subjects.Select(MapToVm));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(TotalStats));
        }
        finally { IsLoading = false; }
    }

    private static SubjectViewModel MapToVm(Subject s) => new()
    {
        Id            = s.Id,
        Name          = s.Name,
        Icon          = s.Icon,
        Color         = s.Color,
        Progress      = s.Progress,
        QuestionCount = s.QuestionCount,
        SuccessRate   = s.SuccessRate,
        Difficulty    = s.Difficulty.ToString(),
        QuestionsToday= s.QuestionsToday,
        LastStudied   = string.IsNullOrEmpty(s.LastStudied) ? "Noch nie" : s.LastStudied,
    };

    [RelayCommand]
    private void SelectSubject(SubjectViewModel s) => SelectedSubject = s;

    [RelayCommand]
    private void AddSubject()
    {
        NewName = string.Empty;
        NewIcon = "∫";
        NewColor = "#5B8CFF";
        NewDifficulty = "Mittel";
        foreach (var item in AvailableIcons) item.IsSelected = false;
        foreach (var item in AvailableColors) item.IsSelected = false;
        ShowAddDialog = true;
    }

    [RelayCommand]
    private void OpenAddDialog() => AddSubjectCommand.Execute(null);

    [RelayCommand]
    private async Task ConfirmAddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName)) return;
        var subject = new Subject
        {
            Name = NewName,
            Icon = NewIcon,
            Color = NewColor,
            Difficulty = ParseDifficulty(NewDifficulty),
            Description = NewDescription,
        };
        await _repo.SaveSubjectAsync(subject);
        Subjects.Add(MapToVm(subject));
        ShowAddDialog = false;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(TotalStats));
    }

    [RelayCommand]
    private void CancelAdd() => ShowAddDialog = false;

    [RelayCommand]
    private void EditSubject(SubjectViewModel s)
    {
        SelectedSubject = s;
        NewName = s.Name;
        NewIcon = s.Icon;
        NewColor = s.Color;
        NewDifficulty = s.Difficulty;
        foreach (var item in AvailableIcons) item.IsSelected = item.Value == s.Icon;
        foreach (var item in AvailableColors) item.IsSelected = item.Value == s.Color;
        ShowEditDialog = true;
    }

    [RelayCommand]
    private async Task ConfirmEditAsync()
    {
        if (SelectedSubject == null || string.IsNullOrWhiteSpace(NewName)) return;
        SelectedSubject.Name = NewName;
        SelectedSubject.Icon = NewIcon;
        SelectedSubject.Color = NewColor;
        SelectedSubject.Difficulty = NewDifficulty;
        await _repo.SaveSubjectAsync(new Subject
        {
            Id = SelectedSubject.Id,
            Name = NewName,
            Icon = NewIcon,
            Color = NewColor,
            Difficulty = ParseDifficulty(NewDifficulty),
            Description = NewDescription,
            Progress = SelectedSubject.Progress,
            QuestionCount = SelectedSubject.QuestionCount,
            SuccessRate = SelectedSubject.SuccessRate,
            QuestionsToday = SelectedSubject.QuestionsToday,
            LastStudied = SelectedSubject.LastStudied,
        });
        ShowEditDialog = false;
    }

    [RelayCommand]
    private void CancelEdit() => ShowEditDialog = false;

    [RelayCommand]
    private void SaveSubject()
    {
        if (ShowAddDialog) ConfirmAddCommand.Execute(null);
        else if (ShowEditDialog) ConfirmEditCommand.Execute(null);
    }

    [RelayCommand]
    private void CancelDialog()
    {
        ShowAddDialog = false;
        ShowEditDialog = false;
    }

    [RelayCommand]
    private async Task DeleteSubjectAsync(SubjectViewModel s)
    {
        await _repo.DeleteSubjectAsync(s.Id);
        Subjects.Remove(s);
        if (SelectedSubject == s) SelectedSubject = null;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(TotalStats));
    }

    [RelayCommand]
    private void SelectIcon(SelectableItem item)
    {
        foreach (var i in AvailableIcons) i.IsSelected = i == item;
        NewIcon = item.Value;
    }

    [RelayCommand]
    private void SelectColor(SelectableItem item)
    {
        foreach (var c in AvailableColors) c.IsSelected = c == item;
        NewColor = item.Value;
    }

    [RelayCommand]
    private void SetColor(string color) => NewColor = color;

    [RelayCommand]
    private void SetIcon(string icon) => NewIcon = icon;

    private static DifficultyLevel ParseDifficulty(string s) => s switch
    {
        "Leicht" => DifficultyLevel.Leicht,
        "Schwer" => DifficultyLevel.Schwer,
        _        => DifficultyLevel.Mittel,
    };
}

public partial class SelectableItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected = false;
    public string Value { get; set; } = string.Empty;
}
