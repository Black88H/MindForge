namespace MindForge.Models;

public class User
{
    public Guid   Id           { get; set; } = Guid.NewGuid();
    public string Username     { get; set; } = string.Empty;
    public string Email        { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int    Level        { get; set; } = 1;
    public int    TotalXP      { get; set; } = 0;
    public int    CurrentStreak{ get; set; } = 0;
    public int    LongestStreak{ get; set; } = 0;
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime? LastLogin  { get; set; }
}
