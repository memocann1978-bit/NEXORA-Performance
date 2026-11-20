using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.ServiceProcess;
using Microsoft.Win32;

namespace NEXORA.App.Services
{
    public sealed class ProcessEntry
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string MemoryMb { get; set; }
        public string WindowTitle { get; set; }
    }

    public sealed class ProcessService
    {
        private static readonly string[] Protected = { "system", "idle", "csrss", "wininit", "winlogon", "services", "lsass", "smss", "svchost", "dwm", "registry", "memory compression" };
        public List<ProcessEntry> GetProcesses()
        {
            return Process.GetProcesses().Select(p =>
            {
                string name = "Unknown", title = "";
                long memory = 0;
                try { name = p.ProcessName; memory = p.WorkingSet64; title = p.MainWindowTitle; } catch { }
                return new ProcessEntry { Id = SafeId(p), Name = name, MemoryMb = (memory / 1048576d).ToString("0.0"), WindowTitle = title };
            }).OrderByDescending(x => double.TryParse(x.MemoryMb, out double m) ? m : 0).ToList();
        }
        private int SafeId(Process p) { try { return p.Id; } catch { return -1; } }
        public void EndProcess(int id)
        {
            using (var p = Process.GetProcessById(id))
            {
                if (Protected.Contains(p.ProcessName.ToLowerInvariant()))
                    throw new InvalidOperationException("Windows için kritik bir işlem koruma nedeniyle sonlandırılamaz.");
                if (p.Id == Process.GetCurrentProcess().Id) throw new InvalidOperationException("NEXORA kendi işlemini sonlandıramaz.");
                p.Kill();
            }
        }
    }

    public sealed class StartupEntry
    {
        public string Name { get; set; }
        public string Command { get; set; }
        public string Location { get; set; }
    }

    public sealed class StartupService
    {
        public List<StartupEntry> GetEntries()
        {
            var result = new List<StartupEntry>();
            ReadRunKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "Kullanıcı");
            ReadRunKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "Bilgisayar");
            return result;
            void ReadRunKey(RegistryKey root, string path, string label)
            {
                try { using (var key = root.OpenSubKey(path)) { if (key == null) return; foreach (var name in key.GetValueNames()) result.Add(new StartupEntry { Name = name, Command = Convert.ToString(key.GetValue(name)), Location = label }); } } catch { }
            }
        }
    }

    public sealed class WindowsServiceEntry
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Status { get; set; }
        public string StartType { get; set; }
    }

    public sealed class WindowsServiceManager
    {
        public List<WindowsServiceEntry> GetServices()
        {
            return ServiceController.GetServices().Select(s => new WindowsServiceEntry { Name = s.ServiceName, DisplayName = s.DisplayName, Status = s.Status.ToString(), StartType = "Windows tarafından yönetiliyor" }).OrderBy(s => s.DisplayName).ToList();
        }
        public void StopService(string serviceName)
        {
            string[] protectedNames = { "WinDefend", "MpsSvc", "wuauserv", "RpcSs", "EventLog", "PlugPlay", "Schedule", "SamSs", "CryptSvc", "BFE" };
            if (protectedNames.Contains(serviceName, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("Kritik veya güvenlik ile ilgili Windows hizmeti NEXORA tarafından durdurulamaz.");
            using (var service = new ServiceController(serviceName))
            {
                if (service.Status == ServiceControllerStatus.Running && service.CanStop)
                {
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                }
                else throw new InvalidOperationException("Bu hizmet durdurulamıyor veya zaten çalışmıyor.");
            }
        }
    }

    public sealed class StorageService
    {
        public string TempPath { get { return Path.GetTempPath(); } }
        public long GetTemporaryFileBytes()
        {
            return Directory.Exists(TempPath) ? Directory.GetFiles(TempPath, "*", SearchOption.AllDirectories).Sum(f => SafeLength(f)) : 0;
        }
        private long SafeLength(string file) { try { return new FileInfo(file).Length; } catch { return 0; } }
        public int CleanTemporaryFiles()
        {
            int count = 0;
            if (!Directory.Exists(TempPath)) return count;
            foreach (var file in Directory.GetFiles(TempPath, "*", SearchOption.AllDirectories))
            {
                try { File.Delete(file); count++; } catch { }
            }
            foreach (var dir in Directory.GetDirectories(TempPath))
            {
                try { Directory.Delete(dir, true); } catch { }
            }
            return count;
        }
    }

    public sealed class NetworkService
    {
        public string GetIpConfiguration()
        {
            var start = new ProcessStartInfo { FileName = "ipconfig.exe", Arguments = "/all", UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
            using (var p = Process.Start(start)) { string output = p.StandardOutput.ReadToEnd(); p.WaitForExit(8000); return output; }
        }
        public void FlushDns()
        {
            var start = new ProcessStartInfo { FileName = "ipconfig.exe", Arguments = "/flushdns", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using (var p = Process.Start(start)) { p.WaitForExit(10000); if (p.ExitCode != 0) throw new InvalidOperationException(p.StandardError.ReadToEnd()); }
        }
    }

    public sealed class GameDetectionService
    {
        private static readonly string[] GameNames = { "gta_sa", "gta5", "gta5_fivem", "FiveM", "FiveM_GTAProcess", "cs2", "csgo", "VALORANT", "VALORANT-Win64-Shipping", "FortniteClient-Win64-Shipping", "r5apex", "League of Legends", "Overwatch", "Minecraft" };
        public List<string> GetRunningGames()
        {
            return Process.GetProcesses().Where(p => GameNames.Any(n => p.ProcessName.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)).Select(p => { try { return p.ProcessName; } catch { return null; } }).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    public sealed class SystemDiagnosticsService
    {
        public string GetSummary()
        {
            var lines = new List<string>();
            lines.Add("İŞLETİM SİSTEMİ: " + Environment.OSVersion);
            lines.Add("BİLGİSAYAR: " + Environment.MachineName);
            lines.Add("MİMARİ: " + (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit"));
            lines.Add("İŞLEMCİ ÇEKİRDEĞİ: " + Environment.ProcessorCount);
            try { using (var q = new ManagementObjectSearcher("SELECT Manufacturer, Model, TotalPhysicalMemory FROM Win32_ComputerSystem")) foreach (ManagementObject o in q.Get()) lines.Add("SİSTEM: " + o["Manufacturer"] + " " + o["Model"] + "\nRAM: " + (Convert.ToDouble(o["TotalPhysicalMemory"]) / 1073741824d).ToString("0.0") + " GB"); } catch { }
            try { using (var q = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController")) foreach (ManagementObject o in q.Get()) lines.Add("GPU: " + o["Name"]); } catch { }
            try { using (var q = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor")) foreach (ManagementObject o in q.Get()) lines.Add("CPU: " + o["Name"] + " | " + o["NumberOfCores"] + " çekirdek / " + o["NumberOfLogicalProcessors"] + " iş parçacığı"); } catch { }
            return string.Join(Environment.NewLine, lines);
        }
    }

    public sealed class AppLogService
    {
        private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NEXORA", "Logs", "nexora.log");
        public string LogPath { get { return _path; } }
        public void Write(string level, string message)
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(_path)); File.AppendAllText(_path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + level + "] " + message + Environment.NewLine); } catch { }
        }
        public string ReadAll() { try { return File.Exists(_path) ? File.ReadAllText(_path) : "Henüz günlük kaydı yok."; } catch (Exception ex) { return ex.Message; } }
    }
}