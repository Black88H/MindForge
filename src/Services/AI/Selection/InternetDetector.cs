using System.Net.Http;

namespace MindForge.Services.AI.Selection;

public class InternetDetector
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private bool? _lastResult;
    private DateTime _lastCheck = DateTime.MinValue;

    public async Task<bool> IsOnlineAsync()
    {
        if (_lastResult.HasValue && (DateTime.UtcNow - _lastCheck).TotalSeconds < 30)
            return _lastResult.Value;

        try
        {
            var resp = await _http.GetAsync("https://dns.google/resolve?name=example.com");
            _lastResult = resp.IsSuccessStatusCode;
        }
        catch
        {
            _lastResult = false;
        }

        _lastCheck = DateTime.UtcNow;
        return _lastResult.Value;
    }
}
