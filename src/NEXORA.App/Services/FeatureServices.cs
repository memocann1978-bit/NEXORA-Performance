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
        private const string UserRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string DisabledRun = @"Software\NEXORA\DisabledStartup";

        public List<StartupEntry> GetEntries()
        {
            var result = new List<StartupEntry>();
            ReadRunKey(Registry.CurrentUser, UserRun, "Kullanıcı");
            ReadRunKey(Registry.LocalMachine, UserRun, "Bilgisayar");
            return result;
        }

        private void ReadRunKey(RegistryKey root, string path, string label)
        {
            try
            {
                using (var key = root.OpenSubKey(path))
                {
                    if (key == null) return;
                    foreach (var name in key.GetValueNames())
                        resultAdd(name, Convert.ToString(key.GetValue(name)), label);
                }
            }
            catch { }
        }

        private readonly List<StartupEntry> _readBuffer = new List<StartupEntry>();
        private void resultAdd(string name, string command, string label)
        {
            _readBuffer.Add(new StartupEntry { Name = name, Command = command, Location = label });
        }

        public List<StartupEntry> GetAllEntries()
        {
            _readBuffer.Clear();
            ReadRunKey(Registry.CurrentUser, UserRun, "Kullanıcı");
            ReadRunKey(Registry.LocalMachine, UserRun, "Bilgisayar");
            return _readBuffer.ToList();
        }

        public void DisableCurrentUserEntry(string name)
        {
            using (var source = Registry.CurrentUser.OpenSubKey(UserRun, true))
            using (var target = Registry.CurrentUser.CreateSubKey(DisabledRun))
            {
                if (source == null) throw new InvalidOperationException("Kullanıcı başlangıç anahtarı bulunamadı.");
                object value = source.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (value == null) throw new InvalidOperationException("Başlangıç girdisi artık mevcut değil.");
                target.SetValue(name, value, source.GetValueKind(name));
                source.DeleteValue(name, false);
            }
        }

        public void RestoreDisabledEntry(string name)
        {
            using (var source = Registry.CurrentUser.OpenSubKey(DisabledRun, true))
            using (var target = Registry.CurrentUser.CreateSubKey(UserRun))
            {
                if (source == null) throw new InvalidOperationException("Devre dışı bırakılmış girdi bulunamadı.");
                object value = source.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (value == null) throw new InvalidOperationException("Geri yüklenecek girdi bulunamadı.");
                target.SetValue(name, value, source.GetValueKind(name));
                source.DeleteValue(name, false);
            }
        }

        public List<string> GetDisabledEntries()
        {
            try { using (var key = Registry.CurrentUser.OpenSubKey(DisabledRun)) return key == null ? new List<string>() : key.GetValueNames().ToList(); }
            catch { return new List<string>(); }
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


    public sealed class GameProcessEntry
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Priority { get; set; }
    }

    public sealed class GameOptimizationService
    {
        private static readonly string[] GameNames = { "gta_sa", "gta5", "gta5_fivem", "FiveM", "FiveM_GTAProcess", "cs2", "csgo", "VALORANT", "VALORANT-Win64-Shipping", "FortniteClient-Win64-Shipping", "r5apex", "League of Legends", "Overwatch", "Minecraft" };

        public List<GameProcessEntry> GetRunningGames()
        {
            var result = new List<GameProcessEntry>();
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (!GameNames.Any(n => process.ProcessName.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                    result.Add(new GameProcessEntry { Id = process.Id, Name = process.ProcessName, Priority = process.PriorityClass.ToString() });
                }
                catch { }
                finally { process.Dispose(); }
            }
            return result.OrderBy(x => x.Name).ToList();
        }

        public void SetHighPriority(int processId)
        {
            using (var process = Process.GetProcessById(processId))
            {
                if (!GameNames.Any(n => process.ProcessName.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0))
                    throw new InvalidOperationException("Bu işlem tanınan bir oyun süreci değil.");
                process.PriorityClass = ProcessPriorityClass.High;
            }
        }

        public void RestoreNormalPriority(int processId)
        {
            using (var process = Process.GetProcessById(processId))
            {
                if (!GameNames.Any(n => process.ProcessName.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0))
                    throw new InvalidOperationException("Bu işlem tanınan bir oyun süreci değil.");
                process.PriorityClass = ProcessPriorityClass.Normal;
            }
        }
    }

    public sealed class GpuDiagnosticsService
    {
        public List<string> GetAdapters()
        {
            var result = new List<string>();
            try
            {
                using (var query = new ManagementObjectSearcher("SELECT Name, DriverVersion, DriverDate, AdapterRAM, VideoProcessor, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate FROM Win32_VideoController"))
                {
                    foreach (ManagementObject item in query.Get())
                    {
                        result.Add("GPU: " + Convert.ToString(item["Name"]));
                        result.Add("Sürücü sürümü: " + (item["DriverVersion"] ?? "Bilinmiyor"));
                        result.Add("Sürücü tarihi: " + (item["DriverDate"] ?? "Bilinmiyor"));
                        try { result.Add("Raporlanan bağdaştırıcı belleği: " + (Convert.ToDouble(item["AdapterRAM"]) / 1073741824d).ToString("0.0") + " GB"); } catch { result.Add("Bağdaştırıcı belleği: Sürücü tarafından raporlanmadı"); }
                        result.Add("Video işlemcisi: " + (item["VideoProcessor"] ?? "Bilinmiyor"));
                        result.Add("Çözünürlük: " + (item["CurrentHorizontalResolution"] ?? "?") + " × " + (item["CurrentVerticalResolution"] ?? "?"));
                        result.Add("Yenileme hızı: " + (item["CurrentRefreshRate"] ?? "Bilinmiyor") + " Hz");
                        result.Add("----------------------------------------");
                    }
                }
            }
            catch (Exception ex) { result.Add("GPU bilgisi okunamadı: " + ex.Message); }
            if (result.Count == 0) result.Add("GPU bağdaştırıcısı bilgisi bulunamadı.");
            return result;
        }
    }


    public sealed class PackageEntry
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Category { get; set; }
    }

    public sealed class PackageManagerService
    {
        public bool IsWingetAvailable()
        {
            try
            {
                var info = new ProcessStartInfo { FileName = "winget.exe", Arguments = "--version", UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (var process = Process.Start(info)) { process.WaitForExit(5000); return process.ExitCode == 0; }
            }
            catch { return false; }
        }

        public async System.Threading.Tasks.Task<string> InstallAsync(string packageId)
        {
            return await RunAsync("install --id " + Quote(packageId) + " --exact --silent --accept-package-agreements --accept-source-agreements");
        }

        public async System.Threading.Tasks.Task<string> UpgradeAllAsync()
        {
            return await RunAsync("upgrade --all --silent --accept-package-agreements --accept-source-agreements");
        }


        public async System.Threading.Tasks.Task<string> UninstallAsync(string packageId)
        {
            return await RunAsync("uninstall --id " + Quote(packageId) + " --exact --silent --accept-source-agreements");
        }

        public async System.Threading.Tasks.Task<string> UpgradeAsync(string packageId)
        {
            return await RunAsync("upgrade --id " + Quote(packageId) + " --exact --silent --accept-package-agreements --accept-source-agreements");
        }

        private string Quote(string value) { return "\"" + value.Replace("\"", "") + "\""; }

        private async System.Threading.Tasks.Task<string> RunAsync(string arguments)
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                var info = new ProcessStartInfo { FileName = "winget.exe", Arguments = arguments, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (var process = Process.Start(info))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    return "Çıkış kodu: " + process.ExitCode + Environment.NewLine + output + (string.IsNullOrWhiteSpace(error) ? "" : Environment.NewLine + error);
                }
            });
        }
    }


    public sealed class PowerPlanService
    {
        private static readonly Dictionary<string, string> Plans = new Dictionary<string, string>
        {
            { "Balanced", "381b4222-f694-41f0-9685-ff5bb260df2e" },
            { "High performance", "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" },
            { "Power saver", "a1841308-3541-4fab-bc81-f71556f20b4a" }
        };

        public string GetPlans()
        {
            return RunPowerCfg("/list");
        }

        public string GetActivePlan()
        {
            return RunPowerCfg("/getactivescheme");
        }

        public string Activate(string name)
        {
            string id;
            if (!Plans.TryGetValue(name, out id)) throw new InvalidOperationException("Desteklenmeyen güç planı.");
            return RunPowerCfg("/setactive " + id);
        }

        private string RunPowerCfg(string args)
        {
            var info = new ProcessStartInfo { FileName = "powercfg.exe", Arguments = args, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output : error);
                return output;
            }
        }
    }

    public sealed class WindowsRepairService
    {
        public async System.Threading.Tasks.Task<string> RunElevatedAsync(string executable, string arguments)
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                var info = new ProcessStartInfo { FileName = executable, Arguments = arguments, UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Normal };
                using (var process = Process.Start(info))
                {
                    process.WaitForExit();
                    return executable + " tamamlandı. Çıkış kodu: " + process.ExitCode;
                }
            });
        }
    }


    public sealed class ReversibleTweakService
    {
        private readonly string _backupPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NEXORA", "Backups", "tweaks-backup.txt");

        private string[] Keys = {
            "Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR|AppCaptureEnabled",
            "System\\GameConfigStore|GameDVR_Enabled"
        };

        public string GetStatus()
        {
            var lines = new List<string>();
            foreach (var entry in Keys)
            {
                var parts = entry.Split('|');
                using (var key = Registry.CurrentUser.OpenSubKey(parts[0], false))
                {
                    object value = key == null ? null : key.GetValue(parts[1], null);
                    lines.Add(parts[1] + ": " + (value == null ? "Windows varsayılanı / tanımsız" : Convert.ToString(value)));
                }
            }
            return string.Join(Environment.NewLine, lines);
        }

        public void DisableGameCapture()
        {
            BackupOnce();
            using (var key = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR")) key.SetValue("AppCaptureEnabled", 0, RegistryValueKind.DWord);
            using (var key = Registry.CurrentUser.CreateSubKey("System\\GameConfigStore")) key.SetValue("GameDVR_Enabled", 0, RegistryValueKind.DWord);
        }

        public void EnableGameCapture()
        {
            BackupOnce();
            using (var key = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR")) key.SetValue("AppCaptureEnabled", 1, RegistryValueKind.DWord);
            using (var key = Registry.CurrentUser.CreateSubKey("System\\GameConfigStore")) key.SetValue("GameDVR_Enabled", 1, RegistryValueKind.DWord);
        }

        public bool HasBackup { get { return File.Exists(_backupPath); } }

        private void BackupOnce()
        {
            if (File.Exists(_backupPath)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(_backupPath));
            var lines = new List<string>();
            foreach (var entry in Keys)
            {
                var parts = entry.Split('|');
                using (var key = Registry.CurrentUser.OpenSubKey(parts[0], false))
                {
                    object value = key == null ? null : key.GetValue(parts[1], null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    RegistryValueKind kind = RegistryValueKind.String;
                    if (key != null && value != null) kind = key.GetValueKind(parts[1]);
                    lines.Add(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(parts[0])) + "|" +
                        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(parts[1])) + "|" +
                        (value == null ? "0" : "1") + "|" + (value == null ? "0" : ((int)kind).ToString()) + "|" +
                        (value == null ? "" : Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Convert.ToString(value)))));
                }
            }
            File.WriteAllLines(_backupPath, lines);
        }

        public void RestoreBackup()
        {
            if (!File.Exists(_backupPath)) throw new InvalidOperationException("Geri yüklenecek tweak yedeği bulunamadı.");
            foreach (var line in File.ReadAllLines(_backupPath))
            {
                var p = line.Split('|');
                string path = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(p[0]));
                string name = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(p[1]));
                using (var key = Registry.CurrentUser.CreateSubKey(path))
                {
                    if (p[2] == "0") key.DeleteValue(name, false);
                    else key.SetValue(name, System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(p[4])), (RegistryValueKind)int.Parse(p[3]));
                }
            }
            File.Delete(_backupPath);
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