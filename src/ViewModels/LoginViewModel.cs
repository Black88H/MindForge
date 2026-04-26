using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindForge.Services;

namespace MindForge.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _auth;

    // ── Commands declared as plain properties ─────────────────────────────────
    // Using [RelayCommand] for LoginAsync/RegisterAsync would emit the property
    // only in source-generator output, which the WPF-internal _wpftmp helper
    // project never sees → MVVMTK0016 / CS0103 build errors.
    // Declaring them here makes them real user-code symbols, visible everywhere.
    public IAsyncRelayCommand LoginAsyncCommand    { get; }
    public IAsyncRelayCommand RegisterAsyncCommand { get; }

    // ── Tab state ─────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isLoginTab    = true;
    [ObservableProperty] private bool _isRegisterTab = false;

    // ── Login fields ─────────────────────────────────────────────────────────
    [ObservableProperty] private string _loginIdentifier = string.Empty;  // username or e-mail
    [ObservableProperty] private string _loginPassword   = string.Empty;  // set from PasswordBox code-behind

    // ── Register fields ───────────────────────────────────────────────────────
    [ObservableProperty] private string _registerUsername = string.Empty;
    [ObservableProperty] private string _registerEmail    = string.Empty;
    [ObservableProperty] private string _registerPassword = string.Empty; // set from PasswordBox code-behind
    [ObservableProperty] private string _registerConfirm  = string.Empty; // set from PasswordBox code-behind

    // ── Status ────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool   _isBusy       = false;

    /// <summary>Raised when login/register succeeded. Window should close with DialogResult=true.</summary>
    public event EventHandler? LoginSuccessful;

    public LoginViewModel(AuthService auth)
    {
        _auth = auth;
        LoginAsyncCommand    = new AsyncRelayCommand(DoLoginAsync,    CanLogin);
        RegisterAsyncCommand = new AsyncRelayCommand(DoRegisterAsync, CanRegister);
    }

    // ── CanExecute notifications ──────────────────────────────────────────────
    // Because LoginAsyncCommand/RegisterAsyncCommand are regular user-code properties
    // (not generated), these partial methods compile fine in _wpftmp too.
    partial void OnLoginIdentifierChanged(string value)  => LoginAsyncCommand.NotifyCanExecuteChanged();
    partial void OnLoginPasswordChanged(string value)    => LoginAsyncCommand.NotifyCanExecuteChanged();
    partial void OnRegisterUsernameChanged(string value) => RegisterAsyncCommand.NotifyCanExecuteChanged();
    partial void OnRegisterEmailChanged(string value)    => RegisterAsyncCommand.NotifyCanExecuteChanged();
    partial void OnRegisterPasswordChanged(string value) => RegisterAsyncCommand.NotifyCanExecuteChanged();

    // ── Tab switch commands (simple → [RelayCommand] is fine here) ────────────
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

    // ── Login logic ───────────────────────────────────────────────────────────
    private bool CanLogin() =>
        !string.IsNullOrWhiteSpace(LoginIdentifier) &&
        !string.IsNullOrWhiteSpace(LoginPassword);

    private async Task DoLoginAsync()
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

    // ── Register logic ────────────────────────────────────────────────────────
    private bool CanRegister() =>
        !string.IsNullOrWhiteSpace(RegisterUsername) &&
        !string.IsNullOrWhiteSpace(RegisterEmail)    &&
        !string.IsNullOrWhiteSpace(RegisterPassword);

    private async Task DoRegisterAsync()
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
                AuthService.RegisterResult.Success       => string.Empty,
                AuthService.RegisterResult.UsernameTaken => "Benutzername bereits vergeben.",
                AuthService.RegisterResult.EmailTaken    => "E-Mail-Adresse bereits registriert.",
                _                                        => "Ein Fehler ist aufgetreten. Bitte versuche es erneut."
            };
            if (result == AuthService.RegisterResult.Success)
                LoginSuccessful?.Invoke(this, EventArgs.Empty);
        }
        finally { IsBusy = false; }
    }
}
