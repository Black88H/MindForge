using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MindForge.Models;
using MindForge.Utils;

namespace MindForge.Services;

public class AuthService
{
    private readonly MindForgeDbContext _db;

    public AuthService(MindForgeDbContext db)
    {
        _db = db;
    }

    // ── Hash ─────────────────────────────────────────────────────────────────

    private static string Hash(string password, string salt)
    {
        var bytes = Encoding.UTF8.GetBytes(password + salt);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    // ── Register ─────────────────────────────────────────────────────────────

    public enum RegisterResult { Success, UsernameTaken, EmailTaken, Error }

    public async Task<RegisterResult> RegisterAsync(string username, string email, string password)
    {
        try
        {
            if (await _db.Users.AnyAsync(u => u.Username == username))
                return RegisterResult.UsernameTaken;

            if (await _db.Users.AnyAsync(u => u.Email == email))
                return RegisterResult.EmailTaken;

            var salt = Guid.NewGuid().ToString("N");
            var user = new User
            {
                Username     = username.Trim(),
                Email        = email.Trim().ToLowerInvariant(),
                PasswordHash = $"{salt}:{Hash(password, salt)}",
                CreatedAt    = DateTime.UtcNow,
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            user.LastLogin = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            UserSession.Login(user);
            return RegisterResult.Success;
        }
        catch
        {
            return RegisterResult.Error;
        }
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    public enum LoginResult { Success, InvalidCredentials, Error }

    public async Task<LoginResult> LoginAsync(string usernameOrEmail, string password)
    {
        try
        {
            var lower = usernameOrEmail.Trim().ToLowerInvariant();
            var user  = await _db.Users.FirstOrDefaultAsync(
                u => u.Username.ToLower() == lower || u.Email == lower);

            if (user is null) return LoginResult.InvalidCredentials;

            // Hash format: "salt:hash"
            var parts = user.PasswordHash.Split(':', 2);
            if (parts.Length != 2) return LoginResult.InvalidCredentials;

            var expected = Hash(password, parts[0]);
            if (parts[1] != expected) return LoginResult.InvalidCredentials;

            user.LastLogin = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            UserSession.Login(user);
            return LoginResult.Success;
        }
        catch
        {
            return LoginResult.Error;
        }
    }
}
