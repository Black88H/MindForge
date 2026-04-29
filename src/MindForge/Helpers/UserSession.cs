using System;
using MindForge.Models;

namespace MindForge.Helpers;

public static class UserSession
{
    public static Guid UserId { get; private set; }
    public static string Username { get; private set; } = string.Empty;
    public static string Email { get; private set; } = string.Empty;
    public static int TotalXP { get; private set; }
    public static int Level { get; private set; }
    public static int CurrentStreak { get; private set; }
    public static int LongestStreak { get; private set; }
    public static bool IsAuthenticated { get; private set; }
    
    public static void Login(User user)
    {
        UserId = user.Id;
        Username = user.Username;
        Email = user.Email;
        TotalXP = user.TotalXP;
        Level = user.Level;
        CurrentStreak = user.CurrentStreak;
        LongestStreak = user.LongestStreak;
        IsAuthenticated = true;
    }
    
    public static void Logout()
    {
        UserId = Guid.Empty;
        Username = string.Empty;
        Email = string.Empty;
        TotalXP = 0;
        Level = 1;
        CurrentStreak = 0;
        LongestStreak = 0;
        IsAuthenticated = false;
    }
    
    public static void UpdateStats(int totalXP, int level, int currentStreak, int longestStreak)
    {
        TotalXP = totalXP;
        Level = level;
        CurrentStreak = currentStreak;
        LongestStreak = longestStreak;
    }
}
