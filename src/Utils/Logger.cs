using System.IO;

namespace MindForge.Utils;

public static class Logger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MindForge", "logs", $"mindforge_{DateTime.Now:yyyyMMdd}.log");

    public static void Info(string message)  => Write("INFO ", message);
    public static void Warn(string message)  => Write("WARN ", message);
    public static void Error(string message, Exception? ex = null)
    {
        Write("ERROR", message);
        if (ex != null) Write("ERROR", ex.ToString());
    }

    private static void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}";
            System.Diagnostics.Debug.WriteLine(line);
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch { /* Logging darf nie crashen */ }
    }
}
