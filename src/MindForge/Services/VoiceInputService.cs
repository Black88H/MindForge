using System;
using System.Speech.Recognition;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

public class VoiceInputService : IVoiceInputService, IDisposable
{
    private SpeechRecognitionEngine? _engine;

    public bool IsListening { get; private set; }
    public bool IsAvailable { get; private set; }

    public event EventHandler<string>? TextRecognized;
    public event EventHandler<string>? PartialResult;

    public VoiceInputService()
    {
        try
        {
            // Use the default system locale (supports German if installed)
            _engine = new SpeechRecognitionEngine();
            _engine.LoadGrammar(new DictationGrammar());
            _engine.SpeechRecognized          += OnSpeechRecognized;
            _engine.SpeechHypothesized        += OnHypothesized;
            _engine.SetInputToDefaultAudioDevice();
            IsAvailable = true;
        }
        catch
        {
            // No microphone or speech engine not available
            IsAvailable = false;
        }
    }

    public void StartListening()
    {
        if (!IsAvailable || IsListening) return;
        try
        {
            _engine!.RecognizeAsync(RecognizeMode.Multiple);
            IsListening = true;
        }
        catch { /* microphone unavailable */ }
    }

    public void StopListening()
    {
        if (!IsListening) return;
        try
        {
            _engine!.RecognizeAsyncStop();
            IsListening = false;
        }
        catch { /* ignore */ }
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        if (e.Result.Confidence > 0.4f)
            TextRecognized?.Invoke(this, e.Result.Text);
    }

    private void OnHypothesized(object? sender, SpeechHypothesizedEventArgs e)
        => PartialResult?.Invoke(this, e.Result.Text);

    public void Dispose()
    {
        StopListening();
        _engine?.Dispose();
        _engine = null;
    }
}
