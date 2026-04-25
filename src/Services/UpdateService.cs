using System.Net.Http;
using System.Text.Json;
using MindForge.Utils;

namespace MindForge.Services;

public class UpdateService
{
    private readonly HttpClient _http;

    public UpdateService()
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("User-Agent", "MindForge-App");
    }

    public async Task<UpdateInfo> CheckForUpdatesAsync()
    {
        try
        {
            var json = await _http.GetStringAsync(Constants.GitHub.ReleasesApi);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString() ?? "v0.0.0";
            var version = tagName.TrimStart('v');
            var body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            var isPrerelease = root.TryGetProperty("prerelease", out var pr) && pr.GetBoolean();
            var downloadUrl = "";

            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() : "";
                    if (name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        downloadUrl = asset.TryGetProperty("browser_download_url", out var u)
                            ? u.GetString() ?? "" : "";
                        break;
                    }
                }
            }

            bool isNewer = IsNewerVersion(version, Constants.AppVersion);

            return new UpdateInfo
            {
                LatestVersion = version,
                CurrentVersion = Constants.AppVersion,
                IsUpdateAvailable = isNewer && !isPrerelease,
                ReleaseNotes = body,
                DownloadUrl = downloadUrl,
                IsStable = !isPrerelease,
                CheckedAt = DateTime.UtcNow
            };
        }
        catch
        {
            return new UpdateInfo
            {
                CurrentVersion = Constants.AppVersion,
                LatestVersion = Constants.AppVersion,
                IsUpdateAvailable = false,
                CheckedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<bool> DownloadUpdateAsync(string downloadUrl, string savePath)
    {
        try
        {
            var bytes = await _http.GetByteArrayAsync(downloadUrl);
            await System.IO.File.WriteAllBytesAsync(savePath, bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsNewerVersion(string remote, string local)
    {
        if (Version.TryParse(remote, out var rv) && Version.TryParse(local, out var lv))
            return rv > lv;
        return false;
    }
}

public class UpdateInfo
{
    public string CurrentVersion { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public bool IsUpdateAvailable { get; set; }
    public bool IsStable { get; set; } = true;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; }
}
