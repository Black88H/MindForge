namespace MindForge.Models;

/// <summary>Tracks AI token consumption per request.</summary>
public class TokenUsage
{
    public Guid     Id               { get; set; } = Guid.NewGuid();
    public Guid     UserId           { get; set; }
    public Guid?    NotebookId       { get; set; }
    /// <summary>"Ollama" | "OpenAI" | "Claude" | "Gemini"</summary>
    public string   Provider         { get; set; } = string.Empty;
    public string   Model            { get; set; } = string.Empty;
    public int      PromptTokens     { get; set; }
    public int      CompletionTokens { get; set; }
    /// <summary>"Chat" | "Summary" | "Quiz" | "Flashcard" | "Embedding"</summary>
    public string   Feature          { get; set; } = string.Empty;
    public DateTime Timestamp        { get; set; } = DateTime.UtcNow;
}
