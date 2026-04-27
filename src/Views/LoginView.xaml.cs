using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
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

        // ── Focus strategy ──────────────────────────────────────────────────
        // WindowChrome (GlassFrameThickness=0) already gives the HWND correct
        // Win32 keyboard routing on Win11.  We still assert Keyboard.Focus here
        // as a belt-and-suspenders measure: it fires after the first layout pass
        // so LoginIdentifierBox is guaranteed to be in the visual tree.
        Loaded += (_, _) =>
        {
            Activate();
            // Normal priority is enough – we're past the layout pass.
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                new Action(() =>
                {
                    LoginIdentifierBox.Focus();
                    Keyboard.Focus(LoginIdentifierBox);
                }));
        };
    }

    // ── Win32 hook: reassert focus whenever the window is activated ─────────
    // This covers the edge case where another process briefly steals focus
    // between the Loaded handler and the user's first keystroke.
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WndProc);
    }

    private const int WM_ACTIVATE   = 0x0006;
    private const int WM_SETFOCUS   = 0x0007;
    private const int WA_INACTIVE   = 0;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_ACTIVATE && wParam.ToInt32() != WA_INACTIVE)
        {
            // Window is being activated – restore keyboard focus to whichever
            // WPF element had it last (WPF does this automatically when the
            // Window has a FocusScope, but an explicit nudge helps Win11).
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
                new Action(() =>
                {
                    var focused = FocusManager.GetFocusedElement(this) as UIElement;
                    focused?.Focus();
                    if (focused != null) Keyboard.Focus(focused);
                }));
        }
        return IntPtr.Zero;
    }

    // ── Drag ────────────────────────────────────────────────────────────────
    // WindowChrome already handles DragMove via the 32 px CaptionHeight area.
    // This handler is kept as a fallback; DragMove() from a left-button-down
    // event is safe even when a caption-level drag is already in progress.
    private void DragBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    // ── PasswordBox bridges ─────────────────────────────────────────────────
    private void LoginPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.LoginPassword = LoginPasswordBox.Password;

    private void RegisterPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.RegisterPassword = RegisterPasswordBox.Password;

    private void RegisterConfirmBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _vm.RegisterConfirm = RegisterConfirmBox.Password;

    // ── Tab focus ───────────────────────────────────────────────────────────
    private void RegisterPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
            {
                RegisterUsernameBox.Focus();
                Keyboard.Focus(RegisterUsernameBox);
            }));
        }
        else
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
            {
                LoginIdentifierBox.Focus();
                Keyboard.Focus(LoginIdentifierBox);
            }));
        }
    }
}
