using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Helpers;
using MindForge.Models;
using MindForge.Services.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MindForge.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly MindForgeDbContext _db;
    private readonly IChatService _chat;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private ObservableCollection<ChatMessage> _messages = new();
    [ObservableProperty] private ObservableCollection<Material> _availableMaterials = new();
    [ObservableProperty] private ObservableCollection<Material> _contextMaterials = new();
    [ObservableProperty] private string _inputText = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _streamingResponse = string.Empty;
    [ObservableProperty] private bool _isStreaming;

    public ChatViewModel(MindForgeDbContext db, IChatService chat)
    {
        _db = db;
        _chat = chat;
    }

    public async Task InitializeAsync()
    {
        var history = await _chat.GetHistoryAsync(UserSession.UserId, 50);
        Messages = new ObservableCollection<ChatMessage>(history);

        var materials = await _db.Materials
            .Where(m => m.UserId == UserSession.UserId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .ToListAsync();
        AvailableMaterials = new ObservableCollection<Material>(materials);
    }

    [RelayCommand]
    public async Task SendMessageAsync()
    {
        var prompt = InputText.Trim();
        if (string.IsNullOrWhiteSpace(prompt) || IsBusy) return;

        InputText = string.Empty;
        IsBusy = true;
        IsStreaming = true;
        StreamingResponse = string.Empty;

        var context = BuildContext();
        _cts = new CancellationTokenSource();
        var sb = new StringBuilder();

        try
        {
            // Optimistically add user message to UI
            Messages.Add(new ChatMessage
            {
                Id = Guid.NewGuid(),
                UserId = UserSession.UserId,
                Content = prompt,
                Role = ChatRole.User,
                CreatedAt = DateTime.UtcNow
            });

            await foreach (var chunk in _chat.StreamMessageAsync(
                UserSession.UserId, prompt, context, _cts.Token))
            {
                sb.Append(chunk);
                StreamingResponse = sb.ToString();
            }

            if (sb.Length > 0)
            {
                Messages.Add(new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    UserId = UserSession.UserId,
                    Content = sb.ToString(),
                    Role = ChatRole.Assistant,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage
            {
                Id = Guid.NewGuid(),
                UserId = UserSession.UserId,
                Content = $"Error: {ex.Message}",
                Role = ChatRole.Assistant,
                CreatedAt = DateTime.UtcNow
            });
        }
        finally
        {
            StreamingResponse = string.Empty;
            IsStreaming = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void StopGeneration() => _cts?.Cancel();

    [RelayCommand]
    public async Task ClearHistoryAsync()
    {
        await _chat.ClearHistoryAsync(UserSession.UserId);
        Messages.Clear();
    }

    public void ToggleContextMaterial(Material material)
    {
        if (ContextMaterials.Contains(material))
            ContextMaterials.Remove(material);
        else
            ContextMaterials.Add(material);
    }

    private string BuildContext()
    {
        if (ContextMaterials.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        foreach (var m in ContextMaterials)
        {
            var excerpt = m.KiContent.Length > 2000
                ? m.KiContent[..2000] + "..."
                : m.KiContent;
            sb.AppendLine($"[{m.OriginalFileName}]");
            sb.AppendLine(excerpt);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
