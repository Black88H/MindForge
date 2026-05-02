using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using MindForge.Models;
using MindForge.ViewModels;

namespace MindForge.Views;

public partial class ChatView : UserControl
{
    private readonly ChatViewModel _vm;

    public ChatView()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<ChatViewModel>();
        DataContext = _vm;

        // Scroll to bottom when new messages arrive
        _vm.Messages.CollectionChanged += (_, _) => Dispatcher.BeginInvoke(ChatScrollViewer.ScrollToBottom);
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_vm.StreamingResponse))
                Dispatcher.BeginInvoke(ChatScrollViewer.ScrollToBottom);
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
        => await _vm.InitializeAsync();

    private async void OnSendClick(object sender, RoutedEventArgs e)
        => await _vm.SendMessageCommand.ExecuteAsync(null);

    private async void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            await _vm.SendMessageCommand.ExecuteAsync(null);
        }
    }

    private void OnStopClick(object sender, RoutedEventArgs e)
        => _vm.StopGenerationCommand.Execute(null);

    private void OnContextMaterialChanged(object sender, SelectionChangedEventArgs e)
    {
        foreach (Material added in e.AddedItems)
            _vm.ToggleContextMaterial(added);
        foreach (Material removed in e.RemovedItems)
            _vm.ToggleContextMaterial(removed);
    }
}
