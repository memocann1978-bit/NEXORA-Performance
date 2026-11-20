using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace NEXORA.App.Services
{
    public sealed class UpdateInfo
    {
        public Version Version { get; set; }
        public string DownloadUrl { get; set; }
    }

    public sealed class UpdateService
    {
        private const string ReleaseApi = "https://api.github.com/repos/memocann1978-bit/NEXORA-Performance/releases/tags/continuous";
        private const string PackageName = "NEXORA-Performance.zip";

        public Version CurrentVersion
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public async Task<UpdateInfo> GetAvailableUpdateAsync()
        {
            return await Task.Run(() =>
            {
                var request = (HttpWebRequest)WebRequest.Create(ReleaseApi);
                request.Method = "GET";
                request.UserAgent = "NEXORA-Performance-Updater";
                request.Accept = "application/vnd.github+json";
                request.Timeout = 10000;
                request.ReadWriteTimeout = 10000;

                string json;
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                    json = reader.ReadToEnd();

                var serializer = new JavaScriptSerializer();
                var release = serializer.Deserialize<Dictionary<string, object>>(json);
                var body = Convert.ToString(release.ContainsKey("body") ? release["body"] : "");
                var match = Regex.Match(body, @"NEXORA_VERSION\s*=\s*([0-9]+(?:\.[0-9]+){1,3})");
                if (!match.Success) throw new InvalidDataException("Yayın sürüm bilgisi bulunamadı.");

                Version latest;
                if (!Version.TryParse(match.Groups[1].Value, out latest))
                    throw new InvalidDataException("Yayın sürüm numarası geçersiz.");

                string downloadUrl = null;
                var assets = release["assets"] as ArrayList;
                if (assets != null)
                {
                    foreach (var item in assets)
                    {
                        var asset = item as Dictionary<string, object>;
                        if (asset == null) continue;
                        if (string.Equals(Convert.ToString(asset["name"]), PackageName, StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = Convert.ToString(asset["browser_download_url"]);
                            break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(downloadUrl))
                    throw new FileNotFoundException("Güncelleme paketi yayında bulunamadı.");

                var uri = new Uri(downloadUrl);
                if (uri.Scheme != Uri.UriSchemeHttps || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Güncelleme adresi güvenli değil.");

                if (latest <= CurrentVersion) return null;
                return new UpdateInfo { Version = latest, DownloadUrl = downloadUrl };
            });
        }

        public async Task DownloadAndApplyAsync(UpdateInfo update)
        {
            if (update == null) throw new ArgumentNullException("update");

            string appDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            string executable = Process.GetCurrentProcess().MainModule.FileName;
            string testFile = Path.Combine(appDirectory, ".nexora-write-test");
            try { File.WriteAllText(testFile, "write"); File.Delete(testFile); }
            catch (Exception ex) { throw new UnauthorizedAccessException("NEXORA klasörü güncellenebilir değil. Uygulamayı kullanıcı klasörüne taşıyıp tekrar deneyin.", ex); }

            await Task.Run(() =>
            {
                string tempRoot = Path.Combine(Path.GetTempPath(), "NEXORA-Update-" + Guid.NewGuid().ToString("N"));
                string zipPath = Path.Combine(tempRoot, PackageName);
                string extractPath = Path.Combine(tempRoot, "package");
                Directory.CreateDirectory(extractPath);

                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.UserAgent] = "NEXORA-Performance-Updater";
                    client.DownloadFile(update.DownloadUrl, zipPath);
                }

                ZipFile.ExtractToDirectory(zipPath, extractPath);
                if (!File.Exists(Path.Combine(extractPath, Path.GetFileName(executable))))
                    throw new InvalidDataException("İndirilen pakette uygulama EXE dosyası bulunamadı.");

                string scriptPath = Path.Combine(Path.GetTempPath(), "NEXORA-apply-" + Guid.NewGuid().ToString("N") + ".cmd");
                string script = "@echo off\r\n" +
                    "set \"APP_PID=" + Process.GetCurrentProcess().Id + "\"\r\n" +
                    ":waitforapp\r\n" +
                    "tasklist /FI \"PID eq %APP_PID%\" | find \"%APP_PID%\" >nul\r\n" +
                    "if not errorlevel 1 (timeout /t 1 /nobreak >nul & goto waitforapp)\r\n" +
                    "xcopy \"" + extractPath + "\\*" + "\" \"" + appDirectory + "\" /E /I /Y >nul\r\n" +
                    "start \"\" \"" + executable + "\"\r\n" +
                    "timeout /t 2 /nobreak >nul\r\n" +
                    "rd /s /q \"" + tempRoot + "\"\r\n" +
                    "del \"%~f0\"\r\n";
                File.WriteAllText(scriptPath, script, Encoding.Default);

                var start = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c \"" + scriptPath + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = appDirectory
                };
                Process.Start(start);
            });
        }
    }
}
