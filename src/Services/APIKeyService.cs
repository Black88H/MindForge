using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Models;

namespace MindForge.Services;

public interface IAPIKeyService
{
    Task SaveKeyAsync(string provider, string apiKey);
    Task<string?> GetKeyAsync(string provider);
    Task DeleteKeyAsync(string provider);
}

public class APIKeyService : IAPIKeyService
{
    private readonly MindForgeDbContext _db;

    public APIKeyService(MindForgeDbContext db) => _db = db;

    public async Task SaveKeyAsync(string provider, string apiKey)
    {
        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(apiKey), null, DataProtectionScope.CurrentUser);
        var base64 = Convert.ToBase64String(encrypted);

        var setting = await _db.Set<SystemSetting>()
            .FirstOrDefaultAsync(s => s.Key == $"apikey:{provider}");

        if (setting != null)
        {
            setting.Value = base64;
        }
        else
        {
            _db.Set<SystemSetting>().Add(new SystemSetting
            {
                Key = $"apikey:{provider}",
                Value = base64
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task<string?> GetKeyAsync(string provider)
    {
        var setting = await _db.Set<SystemSetting>()
            .FirstOrDefaultAsync(s => s.Key == $"apikey:{provider}");

        if (setting?.Value == null) return null;

        try
        {
            var encrypted = Convert.FromBase64String(setting.Value);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return null;
        }
    }

    public async Task DeleteKeyAsync(string provider)
    {
        var setting = await _db.Set<SystemSetting>()
            .FirstOrDefaultAsync(s => s.Key == $"apikey:{provider}");

        if (setting != null)
        {
            _db.Set<SystemSetting>().Remove(setting);
            await _db.SaveChangesAsync();
        }
    }
}
