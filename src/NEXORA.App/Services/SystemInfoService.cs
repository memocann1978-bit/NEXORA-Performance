using System;
using System.Management;
using System.Threading.Tasks;
namespace NEXORA.App.Services
{
    public sealed class SystemInfoService
    {
        public Task<SystemSnapshot> ReadAsync()
        {
            return Task.Run(() =>
            {
                var result = new SystemSnapshot();
                result.OperatingSystem = Environment.OSVersion.VersionString;
                result.ComputerName = Environment.MachineName;
                result.Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT Name, LoadPercentage FROM Win32_Processor"))
                    using (var rows = searcher.Get())
                    {
                        foreach (ManagementObject item in rows)
                        {
                            result.CpuName = Convert.ToString(item["Name"]);
                            if (item["LoadPercentage"] != null) result.CpuUsage = Convert.ToInt32(item["LoadPercentage"]);
                            break;
                        }
                    }
                }
                catch { result.CpuName = "CPU bilgisi alınamadı"; }
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem"))
                    using (var rows = searcher.Get())
                    {
                        foreach (ManagementObject item in rows)
                        {
                            double total = Convert.ToDouble(item["TotalVisibleMemorySize"]);
                            double free = Convert.ToDouble(item["FreePhysicalMemory"]);
                            result.RamTotalGb = total / 1024.0 / 1024.0;
                            result.RamUsedGb = (total - free) / 1024.0 / 1024.0;
                            result.RamUsage = total > 0 ? (int)Math.Round((total - free) / total * 100) : 0;
                            break;
                        }
                    }
                }
                catch { result.RamTotalGb = 0; }
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                    using (var rows = searcher.Get())
                    {
                        foreach (ManagementObject item in rows) { result.GpuName = Convert.ToString(item["Name"]); break; }
                    }
                }
                catch { result.GpuName = "GPU bilgisi alınamadı"; }
                return result;
            });
        }
    }
    public sealed class SystemSnapshot
    {
        public string OperatingSystem { get; set; }
        public string ComputerName { get; set; }
        public string CpuName { get; set; }
        public int CpuUsage { get; set; }
        public double RamTotalGb { get; set; }
        public double RamUsedGb { get; set; }
        public int RamUsage { get; set; }
        public string GpuName { get; set; }
        public TimeSpan Uptime { get; set; }
    }
}
