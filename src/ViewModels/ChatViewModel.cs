using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindForge.Models;
using MindForge.Services;

namespace MindForge.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly IChatService _chat;
    private readonly Guid _userId; // Für MVP statisch aus Session laden
    private Guid? _subjectId;

    [ObservableProperty] private ObservableCollection<ChatMessage> _messages = new();
    [ObservableProperty] private string _inputText = string.Empty;
    [ObservableProperty] private bool _isTyping = false;

    public ChatViewModel(IChatService chat)
    {
        _chat = chat;
        _userId = Utils.UserSession.UserId;
    }

    public async Task InitializeAsync(Guid? subjectId = null)
    {
        _subjectId = subjectId;
        Messages.Clear();
        var history = await _chat.GetChatHistoryAsync(_userId, _subjectId);
        foreach (var msg in history) Messages.Add(msg);
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText)) return;

        var userText = InputText;
        InputText = string.Empty;
        
        Messages.Add(new ChatMessage { Role = ChatRole.User, Content = userText, CreatedAt = DateTime.UtcNow });
        IsTyping = true;

        try
        {
            var response = await _chat.SendMessageAsync(_userId, _subjectId, userText);
            Messages.Add(response);
        }
        finally
        {
            IsTyping = false;
        }
    }

    [RelayCommand]
    private async Task ClearChatAsync()
    {
        await _chat.ClearChatAsync(_userId, _subjectId);
        Messages.Clear();
    }
}
