using MindForge.Services.AI.Models;

namespace MindForge.Services.AI.Selection;

public class TaskAnalyzer
{
    public string[] GetPreferredProviders(TaskType task) => task switch
    {
        TaskType.QAExplanation    => ["Claude", "Gemini", "OpenAI", "Ollama"],
        TaskType.ContentGeneration => ["OpenAI", "Claude", "Gemini", "Ollama"],
        TaskType.TestCreation     => ["Gemini", "Claude", "OpenAI", "Ollama"],
        TaskType.Summary          => ["Claude", "Gemini", "OpenAI", "Ollama"],
        _                         => ["Claude", "OpenAI", "Gemini", "Ollama"],
    };

    public int EstimatedTokens(TaskType task) => task switch
    {
        TaskType.QAExplanation    => 500,
        TaskType.ContentGeneration => 2000,
        TaskType.TestCreation     => 1500,
        TaskType.Summary          => 1000,
        _                         => 1000,
    };
}
