using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using MindForge.Helpers;
using MindForge.Services.Interfaces;

namespace MindForge.Views;

public partial class ChatView : UserControl
{
    private readonly IChatService _chatService;

    public ChatView()
    {
        InitializeComponent();
        
        // Hole IChatService vom DI Container
        _chatService = App.Services.GetRequiredService<IChatService>();
    }

    private async void OnSendClick(object sender, RoutedEventArgs e)
    {
        var prompt = TxtInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt)) return;

        // 1. Füge User-Nachricht ins UI ein
        AddMessageToUI("👤 Du", prompt, "#D1CFFF", "#6C63FF", HorizontalAlignment.Right);
        TxtInput.Text = "";
        BtnSend.IsEnabled = false;

        // 2. Rufe Backend Service auf
        try
        {
            var response = await _chatService.SendMessageAsync(UserSession.UserId, prompt);
            
            // 3. Füge Bot-Nachricht ins UI ein
            AddMessageToUI("🤖 KI-Tutor", response, "#6C63FF", "#1E2640", HorizontalAlignment.Left);
        }
        catch (System.Exception ex)
        {
            AddMessageToUI("❌ Fehler", $"Verbindung zum KI-Tutor fehlgeschlagen: {ex.Message}", "#FF4C61", "#1E2640", HorizontalAlignment.Left);
        }
        finally
        {
            BtnSend.IsEnabled = true;
            ChatScrollViewer.ScrollToBottom();
        }
    }

    private void AddMessageToUI(string senderName, string message, string headerColor, string bgColor, HorizontalAlignment alignment)
    {
        var border = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgColor)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 12),
            HorizontalAlignment = alignment,
            MaxWidth = 600
        };

        var stackPanel = new StackPanel();

        var header = new TextBlock
        {
            Text = senderName,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(headerColor)),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = alignment == HorizontalAlignment.Right ? HorizontalAlignment.Right : HorizontalAlignment.Left
        };

        var content = new TextBlock
        {
            Text = message,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20
        };

        stackPanel.Children.Add(header);
        stackPanel.Children.Add(content);
        border.Child = stackPanel;

        ChatHistoryPanel.Children.Add(border);
        ChatScrollViewer.ScrollToBottom();
    }
}