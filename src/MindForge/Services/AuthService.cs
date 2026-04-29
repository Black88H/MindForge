using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Helpers;
using MindForge.Models;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

public class AuthService : IAuthService
{
    private readonly MindForgeDbContext _db;
    
    public AuthService(MindForgeDbContext db)
    {
        _db = db;
    }
    
    public async Task<Result<User>> RegisterAsync(string username, string email, string password)
    {
        // Validierung
        if (string.IsNullOrWhiteSpace(username))
            return Result<User>.Failure("Username erforderlich");
        
        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
            return Result<User>.Failure("Gültige Email erforderlich");
        
        if (password.Length < 8)
            return Result<User>.Failure("Passwort mind. 8 Zeichen");
        
        // Doppelt-Check
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Result<User>.Failure("Email existiert bereits");
        
        // Passwort hashen
        var salt = Guid.NewGuid().ToString();
        var hash = ComputeHash(password, salt);
        
        var user = new User 
        { 
            Username = username, 
            Email = email, 
            PasswordHash = $"{salt}:{hash}",
            CreatedAt = DateTime.UtcNow
        };
        
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        
        return Result<User>.Success(user);
    }
    
    public async Task<Result<User>> LoginAsync(string email, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
            return Result<User>.Failure("Benutzer nicht gefunden");
        
        // Passwort prüfen
        var parts = user.PasswordHash.Split(':');
        if (parts.Length != 2)
            return Result<User>.Failure("Ungültiges Passwort-Format");
        
        var salt = parts[0];
        var hash = parts[1];
        
        if (!ComputeHash(password, salt).Equals(hash, StringComparison.Ordinal))
            return Result<User>.Failure("Passwort falsch");
        
        // LastLoginAt aktualisieren
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        
        // Session setzen
        UserSession.Login(user);
        
        return Result<User>.Success(user);
    }
    
    public void Logout()
    {
        UserSession.Logout();
    }
    
    private static string ComputeHash(string password, string salt)
    {
        var bytes = Encoding.UTF8.GetBytes(password + salt);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
