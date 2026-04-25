using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace MindForge.ViewModels;

public partial class SubjectsViewModel : ObservableObject
{
    [ObservableProperty] private ObservableCollection<SubjectViewModel> _subjects = new()
    {
        new() { Name="Analysis II",     Icon="∫",    Color="#5B8CFF", Progress=0.68, QuestionCount=342, SuccessRate=0.81, Difficulty="Mittel", QuestionsToday=18, LastStudied="vor 12 Min" },
        new() { Name="Quantenmechanik", Icon="ψ",    Color="#BD93F9", Progress=0.42, QuestionCount=187, SuccessRate=0.73, Difficulty="Schwer", QuestionsToday=0,  LastStudied="Gestern"    },
        new() { Name="English C1",      Icon="En",   Color="#3fcf8e", Progress=0.91, QuestionCount=512, SuccessRate=0.94, Difficulty="Leicht", QuestionsToday=12, LastStudied="vor 2 Std"  },
        new() { Name="Organ. Chemie",   Icon="⌬",    Color="#ffb547", Progress=0.34, QuestionCount=124, SuccessRate=0.67, Difficulty="Schwer", QuestionsToday=0,  LastStudied="vor 3 Tagen"},
        new() { Name="Algorithmen",     Icon="{ }",  Color="#ff6b9d", Progress=0.57, QuestionCount=268, SuccessRate=0.79, Difficulty="Mittel", QuestionsToday=17, LastStudied="vor 25 Min" },
        new() { Name="Genetik",         Icon="◐",    Color="#5eead4", Progress=0.22, QuestionCount=88,  SuccessRate=0.71, Difficulty="Mittel", QuestionsToday=0,  LastStudied="vor 5 Tagen"},
    };

    [ObservableProperty] private SubjectViewModel? _selectedSubject;
    [ObservableProperty] private bool _showAddDialog = false;
    [ObservableProperty] private bool _showEditDialog = false;

    // Unified dialog state (view-facing)
    public bool IsDialogOpen => ShowAddDialog || ShowEditDialog;
    public string DialogTitle => ShowAddDialog ? "Fach hinzufügen" : "Fach bearbeiten";
    public bool IsEmpty => Subjects.Count == 0;

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

    // Edit form (unified)
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private string _newIcon = "∫";
    [ObservableProperty] private string _newColor = "#5B8CFF";
    [ObservableProperty] private string _newDifficulty = "Mittel";
    [ObservableProperty] private string _newDescription = string.Empty;

    // View alias for edit form name
    public string EditName { get => NewName; set => NewName = value; }
    partial void OnNewNameChanged(string value) => OnPropertyChanged(nameof(EditName));

    // Icon picker
    public ObservableCollection<SelectableItem> AvailableIcons { get; } = new(
        new[] { "∫", "ψ", "En", "⌬", "{ }", "◐", "∑", "π", "∞", "α", "🧬", "📈", "⚛", "🔬" }
        .Select(i => new SelectableItem { Value = i }));

    // Color picker
    public ObservableCollection<SelectableItem> AvailableColors { get; } = new(
        new[] { "#5B8CFF", "#BD93F9", "#3fcf8e", "#ffb547", "#ff6b9d", "#5eead4", "#4CAF50", "#FF6B35" }
        .Select(c => new SelectableItem { Value = c }));

    // Legacy lists
    public List<string> PresetColors { get; } = ["#5B8CFF","#BD93F9","#3fcf8e","#ffb547","#ff6b9d","#5eead4","#4CAF50","#FF6B35"];
    public List<string> PresetIcons  { get; } = ["∫","ψ","En","⌬","{ }","◐","∑","π","∞","α","🧬","📈","⚛","🔬"];
    public List<string> Difficulties { get; } = ["Leicht","Mittel","Schwer"];

    public string TotalStats => $"{Subjects.Count} Fächer · {Subjects.Sum(s => s.QuestionCount)} Fragen";

    [RelayCommand]
    private void SelectSubject(SubjectViewModel s) => SelectedSubject = s;

    // Unified add button (view-facing)
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
    private void ConfirmAdd()
    {
        if (string.IsNullOrWhiteSpace(NewName)) return;
        Subjects.Add(new SubjectViewModel
        {
            Name = NewName, Icon = NewIcon, Color = NewColor, Difficulty = NewDifficulty,
            Progress = 0, QuestionCount = 0, SuccessRate = 0, LastStudied = "Noch nie"
        });
        ShowAddDialog = false;
        OnPropertyChanged(nameof(IsEmpty));
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
    private void ConfirmEdit()
    {
        if (SelectedSubject == null || string.IsNullOrWhiteSpace(NewName)) return;
        SelectedSubject.Name = NewName;
        SelectedSubject.Icon = NewIcon;
        SelectedSubject.Color = NewColor;
        SelectedSubject.Difficulty = NewDifficulty;
        ShowEditDialog = false;
    }

    [RelayCommand]
    private void CancelEdit() => ShowEditDialog = false;

    // Unified save/cancel (view-facing)
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
    private void DeleteSubject(SubjectViewModel s)
    {
        Subjects.Remove(s);
        if (SelectedSubject == s) SelectedSubject = null;
        OnPropertyChanged(nameof(IsEmpty));
    }

    // Icon / color selection with IsSelected tracking
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

    // Legacy commands (kept for compatibility)
    [RelayCommand]
    private void SetColor(string color) => NewColor = color;

    [RelayCommand]
    private void SetIcon(string icon) => NewIcon = icon;
}

public partial class SelectableItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected = false;
    public string Value { get; set; } = string.Empty;
}
