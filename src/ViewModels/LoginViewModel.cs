using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindForge.Services;

namespace MindForge.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _auth;

    // ── Tab state ─────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isLoginTab    = true;
    [ObservableProperty] private bool _isRegisterTab = false;

    // ── Login fields ─────────────────────────────────────────────────────────
    [ObservableProperty] private string _loginIdentifier = string.Empty;  // username or e-mail
    [ObservableProperty] private string _loginPassword   = string.Empty;  // set from code-behind

    // ── Register fields ───────────────────────────────────────────────────────
    [ObservableProperty] private string _registerUsername = string.Empty;
    [ObservableProperty] private string _registerEmail    = string.Empty;
    [ObservableProperty] private string _registerPassword = string.Empty; // code-behind
    [ObservableProperty] private string _registerConfirm  = string.Empty; // code-behind

    // ── Status ────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _errorMessage  = string.Empty;
    [ObservableProperty] private bool   _isBusy        = false;

    /// <summary>Raised when login/register succeeded. The window should close with DialogResult=true.</summary>
    public event EventHandler? LoginSuccessful;

    public LoginViewModel(AuthService auth)
    {
        _auth = auth;
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void ShowLogin()
    {
        IsLoginTab    = true;
        IsRegisterTab = false;
        ErrorMessage  = string.Empty;
    }

    [RelayCommand]
    private void ShowRegister()
    {
        IsLoginTab    = false;
        IsRegisterTab = true;
        ErrorMessage  = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;
        IsBusy = true;
        try
        {
            var result = await _auth.LoginAsync(LoginIdentifier, LoginPassword);
            ErrorMessage = result switch
            {
                AuthService.LoginResult.Success            => string.Empty,
                AuthService.LoginResult.InvalidCredentials => "Benutzername oder Passwort falsch.",
                _                                          => "Ein Fehler ist aufgetreten. Bitte versuche es erneut."
            };
            if (result == AuthService.LoginResult.Success)
                LoginSuccessful?.Invoke(this, EventArgs.Empty);
        }
        finally { IsBusy = false; }
    }
    private bool CanLogin() =>
        !string.IsNullOrWhiteSpace(LoginIdentifier) && !string.IsNullOrWhiteSpace(LoginPassword);

    [RelayCommand(CanExecute = nameof(CanRegister))]
    private async Task RegisterAsync()
    {
        if (RegisterPassword != RegisterConfirm)
        {
            ErrorMessage = "Passwörter stimmen nicht überein.";
            return;
        }

        ErrorMessage = string.Empty;
        IsBusy = true;
        try
        {
            var result = await _auth.RegisterAsync(RegisterUsername, RegisterEmail, RegisterPassword);
            ErrorMessage = result switch
            {
                AuthService.RegisterResult.Success      => string.Empty,
                AuthService.RegisterResult.UsernameTaken=> "Benutzername bereits vergeben.",
                AuthService.RegisterResult.EmailTaken   => "E-Mail-Adresse bereits registriert.",
                _                                       => "Ein Fehler ist aufgetreten. Bitte versuche es erneut."
            };
            if (result == AuthService.RegisterResult.Success)
                LoginSuccessful?.Invoke(this, EventArgs.Empty);
        }
        finally { IsBusy = false; }
    }
    private bool CanRegister() =>
        !string.IsNullOrWhiteSpace(RegisterUsername) &&
        !string.IsNullOrWhiteSpace(RegisterEmail)    &&
        !string.IsNullOrWhiteSpace(RegisterPassword);
}
