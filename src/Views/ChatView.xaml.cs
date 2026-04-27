using System.Windows.Controls;
using System.Windows.Input;

namespace MindForge.Views;

public partial class ChatView : UserControl
{
    public ChatView()
    {
        InitializeComponent();
    }

    // Enter → send (Shift+Enter → newline not needed for single-line input)
    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (DataContext is ViewModels.ChatViewModel vm &&
                vm.SendCommand.CanExecute(null))
            {
                vm.SendCommand.Execute(null);
            }
            e.Handled = true;
        }
    }
}
