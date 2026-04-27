using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindForge.Models;
using MindForge.Services;

namespace MindForge.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly LearningPlanService _planService;

    public HomeViewModel(LearningPlanService planService) => _planService = planService;

    // Step tracking
    [ObservableProperty] private int _currentStep = 1;
    [ObservableProperty] private bool _isOnboardingComplete = false;

    // Step 1 — Language
    [ObservableProperty] private string _selectedLanguage = "Deutsch";
    public List<string> Languages { get; } = ["Deutsch", "English"];

    // Step 2 — Account
    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _accountError = string.Empty;

    // Step 3 — Learning Style
    [ObservableProperty] private bool _preferShortExplanations = false;
    [ObservableProperty] private bool _preferLongExplanations = false;
    [ObservableProperty] private bool _preferManyExamples = true;
    [ObservableProperty] private bool _preferExercises = true;
    [ObservableProperty] private bool _preferFormulas = false;
    [ObservableProperty] private string _selectedLearningStyle = "Mixed";
    public List<string> LearningStyles { get; } = ["Visual", "Practice", "Reading", "Mixed"];

    // Step 4 — Subjects
    [ObservableProperty] private List<OnboardingSubject> _suggestedSubjects = new()
    {
        new() { Name = "Mathematik",    Icon = "∑",  Color = "#5B8CFF", IsSelected = false },
        new() { Name = "Physik",        Icon = "⚛",  Color = "#BD93F9", IsSelected = false },
        new() { Name = "Informatik",    Icon = "{ }", Color = "#3fcf8e", IsSelected = false },
        new() { Name = "Englisch",      Icon = "En", Color = "#ffb547", IsSelected = false },
        new() { Name = "Geschichte",    Icon = "📜", Color = "#ff6b9d", IsSelected = false },
        new() { Name = "Chemie",        Icon = "⌬",  Color = "#5eead4", IsSelected = false },
        new() { Name = "Biologie",      Icon = "🧬", Color = "#4CAF50", IsSelected = false },
        new() { Name = "Wirtschaft",    Icon = "📈", Color = "#FF9800", IsSelected = false },
    };
    [ObservableProperty] private string _customSubjectName = string.Empty;

    public List<OnboardingSubject> SelectedSubjects =>
        SuggestedSubjects.Where(s => s.IsSelected).ToList();

    // Step progress
    public int TotalSteps => 5;
    public double StepProgress => (double)CurrentStep / TotalSteps;
    public string StepLabel => $"Schritt {CurrentStep} von {TotalSteps}";

    [RelayCommand]
    private void NextStep()
    {
        if (CurrentStep == 2 && !ValidateAccount()) return;
        if (CurrentStep < TotalSteps)
            CurrentStep++;
        else
            CompleteOnboarding();
    }

    [RelayCommand]
    private void PrevStep()
    {
        if (CurrentStep > 1) CurrentStep--;
    }

    [RelayCommand]
    private void ToggleSubject(OnboardingSubject subject)
    {
        subject.IsSelected = !subject.IsSelected;
        OnPropertyChanged(nameof(SelectedSubjects));
    }

    [RelayCommand]
    private void SelectLanguage(string lang) => SelectedLanguage = lang;

    [RelayCommand]
    private void SelectLearningStyle(string style) => SelectedLearningStyle = style;

    private bool ValidateAccount()
    {
        if (string.IsNullOrWhiteSpace(UserName))
        {
            AccountError = "Bitte gib einen Benutzernamen ein.";
            return false;
        }
        AccountError = string.Empty;
        return true;
    }

    private void CompleteOnboarding()
    {
        IsOnboardingComplete = true;
    }
}

public partial class OnboardingSubject : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _icon = "∫";
    [ObservableProperty] private string _color = "#5B8CFF";
    [ObservableProperty] private bool _isSelected = false;
}
