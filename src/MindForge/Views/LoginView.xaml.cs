using System.Windows;
using System.Windows.Input;
using MindForge.Services.Interfaces;
using MindForge.ViewModels;

namespace MindForge.Views;

public partial class LoginView : Window
{
    private readonly IAuthService _authService;

    public LoginView(IAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    private void OnDragWindow(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

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

    private async void OnLoginClick(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;
        BtnLogin.IsEnabled = false;
        BtnLogin.Content = "Anmelden...";

        var result = await _authService.LoginAsync(TxtEmail.Text.Trim(), PwdPassword.Password);

        if (result.IsSuccess)
        {
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
            // Auto-Login nach Registrierung
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
}
