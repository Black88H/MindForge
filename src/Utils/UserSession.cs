using MindForge.Models;

namespace MindForge.Utils;

/// <summary>
/// Static cross-ViewModel session state. Set once after successful login.
/// </summary>
public static class UserSession
{
    public static Guid   UserId        { get; private set; }
    public static string Username      { get; private set; } = string.Empty;
    public static string Email         { get; private set; } = string.Empty;
    public static int    Level         { get; private set; } = 1;
    public static int    TotalXP       { get; private set; } = 0;
    public static int    CurrentStreak { get; private set; } = 0;
    public static int    LongestStreak { get; private set; } = 0;
    public static bool   IsLoggedIn    { get; private set; } = false;

    public static void Login(User user)
    {
        UserId        = user.Id;
        Username      = user.Username;
        Email         = user.Email;
        Level         = user.Level;
        TotalXP       = user.TotalXP;
        CurrentStreak = user.CurrentStreak;
        LongestStreak = user.LongestStreak;
        IsLoggedIn    = true;
    }

    public static void Logout()
    {
        UserId        = Guid.Empty;
        Username      = string.Empty;
        Email         = string.Empty;
        Level         = 1;
        TotalXP       = 0;
        CurrentStreak = 0;
        LongestStreak = 0;
        IsLoggedIn    = false;
    }
}
