namespace MindForge.Services.AI.Selection;

public class HardwareDetector
{
    private long? _totalRamMB;

    public long GetTotalRAMMB()
    {
        if (_totalRamMB.HasValue) return _totalRamMB.Value;

        try
        {
            var info = GC.GetGCMemoryInfo();
            _totalRamMB = Math.Max(2048, info.TotalAvailableMemoryBytes / 1024 / 1024);
        }
        catch
        {
            _totalRamMB = 8192;
        }

        return _totalRamMB.Value;
    }

    public string GetRecommendedOfflineModel()
    {
        var ram = GetTotalRAMMB();
        return ram switch
        {
            >= 16384 => "mistral",    // 16 GB+ → Mistral 7B
            >= 8192  => "phi3",       // 8 GB+  → Phi-3
            _        => "tinyllama",  // <8 GB  → TinyLlama
        };
    }
}
