using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
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

        // Listen for property changes on the ViewModel
        _vm.PropertyChanged += OnVmPropertyChanged;

        // Subscribe to the initial Messages collection
        SubscribeToMessages();
    }

    // ── Scroll management ─────────────────────────────────────────────────────

    private void SubscribeToMessages()
        => _vm.Messages.CollectionChanged += OnMessagesChanged;

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => ScrollToBottom();

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            // When the whole Messages collection is replaced (e.g. history load),
            // re-subscribe to the new collection and scroll after layout pass.
            case nameof(_vm.Messages):
                SubscribeToMessages();
                ScrollToBottom();
                break;

            // Each new streaming token → scroll to keep the live bubble visible
            case nameof(_vm.StreamingResponse):
            case nameof(_vm.IsStreaming):
                ScrollToBottom();
                break;
        }
    }

    /// Schedules a scroll-to-bottom at Background priority so WPF has
    /// finished its layout pass before we move the scrollbar.
    private void ScrollToBottom()
        => Dispatcher.BeginInvoke(DispatcherPriority.Background,
            new Action(ChatScrollViewer.ScrollToBottom));

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _vm.InitializeAsync();
        // Scroll after history renders
        ScrollToBottom();
    }

    // ── Button / keyboard handlers ────────────────────────────────────────────

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
