using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using MindForge.Services.Interfaces;

namespace MindForge.Views;

public partial class LoginView : Window
{
    private readonly IAuthService _authService;
    private const string SessionFile = "session.json";

    private static string SessionFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MindForge", SessionFile);

    public LoginView(IAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        CheckSavedSession();
    }

    // ── AUTO-LOGIN (Remember Me) ─────────────────────────────────────────────

    private async void CheckSavedSession()
    {
        try
        {
            if (!File.Exists(SessionFilePath)) return;
            var json = JsonDocument.Parse(await File.ReadAllTextAsync(SessionFilePath));
            if (!json.RootElement.TryGetProperty("email", out var emailEl)) return;
            if (!json.RootElement.TryGetProperty("password", out var pwdEl)) return;

            var email = emailEl.GetString() ?? "";
            var pwd = pwdEl.GetString() ?? "";
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pwd)) return;

            var result = await _authService.LoginAsync(email, pwd);
            if (result.IsSuccess)
            {
                var mainWindow = new MainWindow(App.Services);
                mainWindow.Show();
                Close();
            }
        }
        catch { /* Falls Session ungültig → normaler Login */ }
    }

    // ── DRAG & CLOSE ──────────────────────────────────────────────────────────

    private void OnDragWindow(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    // ── TABS ──────────────────────────────────────────────────────────────────

    private void OnLoginTabClick(object sender, RoutedEventArgs e)
    {
        LoginPanel.Visibility = Visibility.Visible;
        RegisterPanel.Visibility = Visibility.Collapsed;
        BtnLoginTab.BorderThickness = new Thickness(0, 0, 0, 2);
        BtnRegisterTab.BorderThickness = new Thickness(0);
    }

    private void OnRegisterTabClick(object sender, RoutedEventArgs e)
    {
        LoginPanel.Visibility = Visibility.Collapsed;
        RegisterPanel.Visibility = Visibility.Visible;
        BtnRegisterTab.BorderThickness = new Thickness(0, 0, 0, 2);
        BtnLoginTab.BorderThickness = new Thickness(0);
    }

    // ── LOGIN ─────────────────────────────────────────────────────────────────

    private async void OnLoginClick(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;
        BtnLogin.IsEnabled = false;
        BtnLogin.Content = "Anmelden...";

        var email = TxtEmail.Text.Trim();
        var password = PwdPassword.Password;

        var result = await _authService.LoginAsync(email, password);

        if (result.IsSuccess)
        {
            // "Remember Me" speichern
            if (ChkRememberMe.IsChecked == true)
                SaveSession(email, password);
            else
                DeleteSession();

            var mainWindow = new MainWindow(App.Services);
            mainWindow.Show();
            Close();
        }
        else
        {
            TxtError.Text = result.ErrorMessage;
            TxtError.Visibility = Visibility.Visible;
            BtnLogin.IsEnabled = true;
            BtnLogin.Content = "Anmelden";
        }
    }

    // ── REGISTER ──────────────────────────────────────────────────────────────

    private async void OnRegisterClick(object sender, RoutedEventArgs e)
    {
        TxtRegError.Visibility = Visibility.Collapsed;
        BtnRegister.IsEnabled = false;
        BtnRegister.Content = "Wird erstellt...";

        var result = await _authService.RegisterAsync(
            TxtRegUsername.Text.Trim(),
            TxtRegEmail.Text.Trim(),
            PwdRegPassword.Password);

        if (result.IsSuccess)
        {
            var loginResult = await _authService.LoginAsync(TxtRegEmail.Text.Trim(), PwdRegPassword.Password);
            if (loginResult.IsSuccess)
            {
                var mainWindow = new MainWindow(App.Services);
                mainWindow.Show();
                Close();
                return;
            }
        }

        TxtRegError.Text = result.ErrorMessage;
        TxtRegError.Visibility = Visibility.Visible;
        BtnRegister.IsEnabled = true;
        BtnRegister.Content = "Konto erstellen";
    }

    // ── SESSION HELPERS ───────────────────────────────────────────────────────

    private static void SaveSession(string email, string password)
    {
        try
        {
            var dir = Path.GetDirectoryName(SessionFilePath)!;
            Directory.CreateDirectory(dir);
            var json = $"{{\"email\":\"{email}\",\"password\":\"{password}\"}}";
            File.WriteAllText(SessionFilePath, json);
        }
        catch { }
    }

    private static void DeleteSession()
    {
        try { if (File.Exists(SessionFilePath)) File.Delete(SessionFilePath); }
        catch { }
    }
}
