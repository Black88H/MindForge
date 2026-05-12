using System;
using System.Threading.Tasks;

namespace MindForge.Services.Interfaces;

public interface IVoiceInputService
{
    bool IsListening { get; }
    bool IsAvailable { get; }
    event EventHandler<string> TextRecognized;
    event EventHandler<string> PartialResult;
    void StartListening();
    void StopListening();
}
