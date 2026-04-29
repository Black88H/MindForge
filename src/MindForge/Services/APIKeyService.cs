using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

public class APIKeyService : IAPIKeyService
{
    private readonly MindForgeDbContext _db;
    private readonly byte[] _entropy = Encoding.UTF8.GetBytes("MindForge_API_Key_Entropy_2026");

    public APIKeyService(MindForgeDbContext db)
    {
        _db = db;
    }

    public async Task<string?> GetApiKeyAsync(string provider)
    {
        var keyName = $"APIKey_{provider.ToUpperInvariant()}";
        var setting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == keyName);
        
        if (setting == null || string.IsNullOrEmpty(setting.Value))
            return null;

        try
        {
            var encryptedBytes = Convert.FromBase64String(setting.Value);
            var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, _entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch
        {
            return null; // Decryption failed
        }
    }

    public async Task SaveApiKeyAsync(string provider, string apiKey)
    {
        var keyName = $"APIKey_{provider.ToUpperInvariant()}";
        
        var plainBytes = Encoding.UTF8.GetBytes(apiKey);
        var encryptedBytes = ProtectedData.Protect(plainBytes, _entropy, DataProtectionScope.CurrentUser);
        var encryptedString = Convert.ToBase64String(encryptedBytes);

        var setting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == keyName);
        if (setting == null)
        {
            setting = new AppSettings { Key = keyName, Value = encryptedString };
            _db.AppSettings.Add(setting);
        }
        else
        {
            setting.Value = encryptedString;
            _db.AppSettings.Update(setting);
        }

        await _db.SaveChangesAsync();
    }

    public async Task<bool> HasApiKeyAsync(string provider)
    {
        var key = await GetApiKeyAsync(provider);
        return !string.IsNullOrWhiteSpace(key);
    }
}
