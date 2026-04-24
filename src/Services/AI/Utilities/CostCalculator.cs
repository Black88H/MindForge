namespace MindForge.Services.AI.Utilities;

public static class CostCalculator
{
    // USD per 1M tokens (input, output) — Stand 2024/2025
    private static readonly Dictionary<string, (double In, double Out)> Rates = new()
    {
        ["Claude"]  = (0.80,  4.00),   // claude-3-5-haiku
        ["OpenAI"]  = (0.15,  0.60),   // gpt-4o-mini
        ["Gemini"]  = (0.075, 0.30),   // gemini-1.5-flash
        ["Ollama"]  = (0.0,   0.0),    // lokal — kostenlos
    };

    public static double Calculate(string provider, int inputTokens, int outputTokens)
    {
        if (!Rates.TryGetValue(provider, out var r)) return 0;
        return (inputTokens * r.In + outputTokens * r.Out) / 1_000_000.0;
    }

    public static string FormatCost(double usd) =>
        usd < 0.001 ? "< $0.001" : $"${usd:F4}";
}
