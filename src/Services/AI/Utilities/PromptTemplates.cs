namespace MindForge.Services.AI.Utilities;

public static class PromptTemplates
{
    public static string QAExplanationPrompt(string question, string context = "") =>
        $"""
        Du bist ein präziser Lernassistent. Erkläre die folgende Frage verständlich auf Deutsch.

        Frage: {question}
        {(string.IsNullOrEmpty(context) ? "" : $"\nKontext: {context}")}

        Antworte in 3-4 Sätzen. Nutze ein konkretes Beispiel. Formatiere als Markdown.
        """;

    public static string ContentGenerationPrompt(string text, string contentType) =>
        contentType switch
        {
            "Fragen" =>
                $"""
                Analysiere den Text und erstelle 10–15 Multiple-Choice-Lernfragen auf Deutsch.
                Format je Frage:
                **Frage X:** [Frage]
                - A) ...
                - B) ...
                - ✓ C) [richtige Antwort]
                - D) ...

                Text:
                {text}
                """,

            "Zusammenfassung" =>
                $"""
                Erstelle eine strukturierte Zusammenfassung auf Deutsch.
                Gliederung: Hauptthemen → Schlüsselkonzepte → Wichtigste Erkenntnisse.
                Formatiere als Markdown.

                Text:
                {text}
                """,

            _ =>
                $"""
                Verarbeite den Text und erstelle Lernmaterial auf Deutsch. Formatiere als Markdown.

                Text:
                {text}
                """
        };
}
