using System.Windows;
using System.Windows.Input;

namespace MindForge;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        KeyDown += (_, e) =>
        {
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                var vm = DataContext as ViewModels.MainViewModel;
                switch (e.Key)
                {
                    case Key.D1: vm?.NavigateToCommand.Execute("QA"); break;
                    case Key.D2: vm?.NavigateToCommand.Execute("ContentGenerator"); break;
                    case Key.D3: vm?.NavigateToCommand.Execute("TestCreator"); break;
                    case Key.D4: vm?.NavigateToCommand.Execute("Analytics"); break;
                    case Key.OemComma: vm?.NavigateToCommand.Execute("Settings"); break;
                    case Key.B: vm?.ToggleSidebarCommand.Execute(null); break;
                }
            }
        };
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else
            DragMove();
    }
}
