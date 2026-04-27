using System.Windows;
using System.Windows.Input;
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

        // Win11 + WindowStyle=None: focus must be reasserted after the window
        // is fully shown, otherwise the first keystroke is dropped. We do it
        // on ContentRendered (fires once after first layout pass) at Input
        // priority so the WPF input system has finished initializing.
        ContentRendered += (_, _) =>
        {
            Activate();
            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Input,
                new Action(() =>
                {
                    LoginIdentifierBox.Focus();
                    Keyboard.Focus(LoginIdentifierBox);
                }));
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
