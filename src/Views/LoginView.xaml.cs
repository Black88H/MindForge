using System.Windows;
using MindForge.ViewModels;

namespace MindForge.Views;

public partial class LoginView : Window
{
    private readonly LoginViewModel _vm;

    public LoginView(LoginViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        vm.LoginSuccessful += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }

    // ── Drag ─────────────────────────────────────────────────────────────────
    private void DragBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            DragMove();
    }

    // ── PasswordBox bridges ───────────────────────────────────────────────────
    private void LoginPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.LoginPassword = LoginPasswordBox.Password;

    private void RegisterPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.RegisterPassword = RegisterPasswordBox.Password;

    private void RegisterConfirmBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.RegisterConfirm = RegisterConfirmBox.Password;
}
