using System.Management;

namespace MindForge.Utils;

public class HardwareDetector
{
    public static HardwareInfo Detect()
    {
        var info = new HardwareInfo();
        try
        {
            using var ramQuery = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
            foreach (var obj in ramQuery.Get())
                info.TotalRAMGB = Math.Round(Convert.ToDouble(obj["TotalVisibleMemorySize"]) / 1024 / 1024, 1);

            using var cpuQuery = new ManagementObjectSearcher("SELECT Name, NumberOfCores FROM Win32_Processor");
            foreach (var obj in cpuQuery.Get())
            {
                info.CpuName = obj["Name"]?.ToString() ?? "Unknown";
                info.CpuCores = Convert.ToInt32(obj["NumberOfCores"]);
            }

            info.ScreenWidth  = (int)SystemParameters.PrimaryScreenWidth;
            info.ScreenHeight = (int)SystemParameters.PrimaryScreenHeight;
        }
        catch { }
        return info;
    }

    public static UsageInfo GetCurrentUsage()
    {
        var usage = new UsageInfo();
        try
        {
            using var cpuCounter  = new System.Diagnostics.PerformanceCounter("Processor", "% Processor Time", "_Total");
            using var ramQuery    = new ManagementObjectSearcher("SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem");
            cpuCounter.NextValue();
            System.Threading.Thread.Sleep(100);
            usage.CpuPercent = (int)cpuCounter.NextValue();

            foreach (var obj in ramQuery.Get())
            {
                var free  = Convert.ToDouble(obj["FreePhysicalMemory"]);
                var total = Convert.ToDouble(obj["TotalVisibleMemorySize"]);
                usage.RamPercent = total > 0 ? (int)((1 - free / total) * 100) : 0;
            }
        }
        catch { }
        return usage;
    }
}

// Need to reference System.Windows (PresentationFramework) for SystemParameters
file static class SystemParameters
{
    public static double PrimaryScreenWidth  => System.Windows.SystemParameters.PrimaryScreenWidth;
    public static double PrimaryScreenHeight => System.Windows.SystemParameters.PrimaryScreenHeight;
}

public class HardwareInfo
{
    public double TotalRAMGB { get; set; }
    public string CpuName { get; set; } = string.Empty;
    public int CpuCores { get; set; }
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
    public string Summary => $"RAM: {TotalRAMGB:F0} GB  ·  CPU: {CpuName}  ·  Auflösung: {ScreenWidth}×{ScreenHeight}";
}

public class UsageInfo
{
    public int CpuPercent { get; set; }
    public int RamPercent { get; set; }
}
