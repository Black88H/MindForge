using System;
using System.Threading.Tasks;

namespace MindForge.Services.Interfaces;

public interface IChatService
{
    Task<string> SendMessageAsync(Guid userId, string prompt, string context = "");
}
