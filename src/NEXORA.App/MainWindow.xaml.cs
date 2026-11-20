using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NEXORA.App.Services;

namespace NEXORA.App
{
    public partial class MainWindow : Window
    {
        private readonly SystemInfoService _systemInfo = new SystemInfoService();
        private readonly UpdateService _updateService = new UpdateService();
        private readonly ProcessService _processService = new ProcessService();
        private readonly StartupService _startupService = new StartupService();
        private readonly WindowsServiceManager _windowsServices = new WindowsServiceManager();
        private readonly StorageService _storageService = new StorageService();
        private readonly NetworkService _networkService = new NetworkService();
        private readonly GameDetectionService _gameDetection = new GameDetectionService();
        private readonly GameOptimizationService _gameOptimizer = new GameOptimizationService();
        private readonly GpuDiagnosticsService _gpuDiagnostics = new GpuDiagnosticsService();
        private readonly SystemDiagnosticsService _diagnostics = new SystemDiagnosticsService();
        private readonly AppLogService _appLog = new AppLogService();
        private readonly PackageManagerService _packageManager = new PackageManagerService();
        private readonly WindowsRepairService _repairService = new WindowsRepairService();
        private readonly PowerPlanService _powerPlans = new PowerPlanService();
        private readonly ReversibleTweakService _tweaks = new ReversibleTweakService();
        private readonly DispatcherTimer _refreshTimer = new DispatcherTimer();
        private readonly List<UIElement> _dashboardElements = new List<UIElement>();
        private bool _refreshInProgress;
        private string _currentPageKey = "dashboard";

        public MainWindow()
        {
            InitializeComponent();
            foreach (UIElement element in PageContent.Children)
                _dashboardElements.Add(element);

            Loaded += async (s, e) => { await RefreshSystemInfoAsync(); await CheckForUpdatesAsync(false); };
            _refreshTimer.Interval = TimeSpan.FromSeconds(5);
            _refreshTimer.Tick += async (s, e) => await RefreshSystemInfoAsync();
            _refreshTimer.Start();
            Closed += (s, e) => _refreshTimer.Stop();
        }

        private async Task RefreshSystemInfoAsync()
        {
            if (_refreshInProgress) return;
            _refreshInProgress = true;
            try
            {
                var data = await _systemInfo.ReadAsync();
                CpuValue.Text = data.CpuUsage + "%";
                CpuName.Text = string.IsNullOrWhiteSpace(data.CpuName) ? "Kullanılamıyor" : data.CpuName;
                RamValue.Text = data.RamUsage + "%";
                RamDetail.Text = data.RamUsedGb.ToString("0.0") + " / " + data.RamTotalGb.ToString("0.0") + " GB";
                GpuValue.Text = string.IsNullOrWhiteSpace(data.GpuName) ? "Kullanılamıyor" : data.GpuName;
                OsValue.Text = data.OperatingSystem;
                PcValue.Text = data.ComputerName;
                UptimeValue.Text = ((int)data.Uptime.TotalDays) + " gün " + data.Uptime.Hours + " saat";
            }
            catch (Exception ex)
            {
                PageSubtitle.Text = "Sistem bilgisi okunamadı: " + ex.Message;
            }
            finally { _refreshInProgress = false; }
        }

        private void Navigate_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;
            var key = Convert.ToString(button.Tag);
            _currentPageKey = key;
            var titles = new Dictionary<string, string>
            {
                {"dashboard","Genel Bakış"},{"boost","Quick Boost"},{"gaming","Gaming Center"},{"apps","Uygulama Yöneticisi"},{"tweaks","Windows Tweaks"},{"repair","Windows Onarım"},{"features","Windows Özellikleri"},{"power","Güç Planları"},{"updates","Windows Update"},{"config","Sistem Yapılandırması"},{"debloat","Debloat Manager"},
                {"hardware","Donanım İzleme"},{"processes","İşlem Yöneticisi"},
                {"startup","Başlangıç Uygulamaları"},{"services","Windows Hizmetleri"},{"storage","Depolama Temizliği"},{"network","Ağ Merkezi"},{"gpu","GPU Merkezi"},{"benchmark","Performans Testi"},{"logs","Sistem Günlükleri"},{"restore","Geri Yükleme"},{"windows","Windows Araçları"},{"settings","Ayarlar"}
            };

            PageTitle.Text = titles.ContainsKey(key) ? titles[key] : "NEXORA";
            PageContent.Children.Clear();

            if (key == "dashboard")
            {
                PageSubtitle.Text = "Sisteminin durumunu tek merkezden takip et.";
                foreach (UIElement element in _dashboardElements) PageContent.Children.Add(element);
                return;
            }

            PageSubtitle.Text = "NEXORA Performance • " + (key == "windows" ? "Yerleşik Windows araçları" : "Preview");
            if (key == "windows") BuildWindowsToolsPage();
            else if (key == "settings") BuildSettingsPage();
            else if (key == "hardware" || key == "gpu") BuildHardwarePage();
            else if (key == "processes") BuildProcessesPage();
            else if (key == "startup") BuildStartupPage();
            else if (key == "services") BuildServicesPage();
            else if (key == "storage") BuildStoragePage();
            else if (key == "network") BuildNetworkPage();
            else if (key == "benchmark") BuildBenchmarkPage();
            else if (key == "logs") BuildLogsPage();
            else if (key == "restore") BuildRestorePage();
            else if (key == "boost") BuildQuickBoostPage();
            else if (key == "gaming") BuildGamingPage();
            else if (key == "apps") BuildAppsPage();
            else if (key == "tweaks") BuildTweaksPage();
            else if (key == "repair") BuildRepairPage();
            else if (key == "features") BuildFeaturesPage();
            else if (key == "power") BuildPowerPage();
            else if (key == "updates") BuildUpdatesPage();
            else if (key == "config") BuildConfigPage();
            else if (key == "debloat") BuildDebloatPage();
            else BuildComingSoonPage(key, titles.ContainsKey(key) ? titles[key] : "Modül");
        }

        private void BuildComingSoonPage(string key, string title)
        {
            string message;
            switch (key)
            {
                case "boost":
                    message = "Quick Boost profilleri hazırlanıyor. Bu sürüm henüz sistem ayarlarını değiştirmez; güvenli profil, işlem öncesi açıklama ve geri alma desteği tamamlandığında etkinleştirilecek.";
                    break;
                case "gaming":
                    message = "Oyun profilleri, oyun algılama ve oturum bazlı performans araçları geliştirme aşamasında.";
                    break;
                case "processes":
                    message = "İşlem görüntüleme ve kaynak sıralama ekranı hazırlanıyor. İşlemleri zorla kapatma özelliği henüz etkin değil.";
                    break;
                case "startup":
                    message = "Başlangıç uygulamalarını güvenli biçimde listeleme ve kullanıcı onayıyla yönetme ekranı hazırlanıyor.";
                    break;
                default:
                    message = "Bu modül geliştirme yol haritasında. Henüz etkin olmayan özellikler tamamlanmış gibi gösterilmez.";
                    break;
            }
            AddInfoCard(title, message);
        }



        private void BuildAppsPage()
        {
            AddInfoCard("Uygulama Yöneticisi • WinGet", "Uygulamaları tek merkezden kur, seçili uygulamaları güncelle veya tüm paketleri yükselt. Kurulumlar Microsoft WinGet üzerinden yapılır; NEXORA üçüncü taraf kurulum dosyalarını barındırmaz.");
            if (!_packageManager.IsWingetAvailable())
            {
                AddInfoCard("WinGet bulunamadı", "Uygulama kurulumları için Windows App Installer / WinGet gerekir. Microsoft Store'dan App Installer yükleyip bu sayfayı yenile.");
                AddActionButton("Microsoft App Installer sayfasını aç", () => Launch("ms-windows-store://pdp/?ProductId=9NBLGGH4NNS1"));
                AddActionButton("WinGet belgelerini aç", () => Launch("https://learn.microsoft.com/windows/package-manager/winget/"));
                return;
            }
            AddInfoCard("Paket yöneticisi hazır", "WinGet kullanılabilir. Bir uygulama seçip yükleyebilirsin. Bazı paketler yönetici izni veya etkileşimli onay isteyebilir.");
            var search = new TextBox { Height = 36, Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 10), Background = (Brush)FindResource("PanelBrush"), Foreground = (Brush)FindResource("TextBrush"), BorderThickness = new Thickness(0), ToolTip = "Uygulama ara" };
            PageContent.Children.Add(search);
            var packages = new List<PackageEntry>
            {
                new PackageEntry{Name="7-Zip",Id="7zip.7zip",Category="Araçlar"},
                new PackageEntry{Name="Google Chrome",Id="Google.Chrome",Category="Tarayıcı"},
                new PackageEntry{Name="Mozilla Firefox",Id="Mozilla.Firefox",Category="Tarayıcı"},
                new PackageEntry{Name="Brave",Id="Brave.Brave",Category="Tarayıcı"},
                new PackageEntry{Name="Opera",Id="Opera.Opera",Category="Tarayıcı"},
                new PackageEntry{Name="VLC Media Player",Id="VideoLAN.VLC",Category="Medya"},
                new PackageEntry{Name="Discord",Id="Discord.Discord",Category="İletişim"},
                new PackageEntry{Name="Steam",Id="Valve.Steam",Category="Oyun"},
                new PackageEntry{Name="Epic Games Launcher",Id="EpicGames.EpicGamesLauncher",Category="Oyun"},
                new PackageEntry{Name="OBS Studio",Id="OBSProject.OBSStudio",Category="Medya"},
                new PackageEntry{Name="Visual Studio Code",Id="Microsoft.VisualStudioCode",Category="Geliştirme"},
                new PackageEntry{Name="Git",Id="Git.Git",Category="Geliştirme"},
                new PackageEntry{Name="Notepad++",Id="Notepad++.Notepad++",Category="Geliştirme"},
                new PackageEntry{Name="Microsoft PowerToys",Id="Microsoft.PowerToys",Category="Araçlar"},
                new PackageEntry{Name="ShareX",Id="ShareX.ShareX",Category="Araçlar"},
                new PackageEntry{Name="Spotify",Id="Spotify.Spotify",Category="Medya"}
            };
            var list = new ListBox { Height = 340, Background = (Brush)FindResource("PanelBrush"), Foreground = (Brush)FindResource("TextBrush"), BorderThickness = new Thickness(0), SelectionMode = SelectionMode.Extended };
            PageContent.Children.Add(list);
            Action populate = () =>
            {
                list.Items.Clear();
                string term = search.Text.Trim();
                foreach (var package in packages.Where(p => string.IsNullOrEmpty(term) || p.Name.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0 || p.Category.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0))
                    list.Items.Add(new ListBoxItem { Content = package.Name + "   •   " + package.Category + "   |   " + package.Id, Tag = package, Padding = new Thickness(8) });
            };
            search.TextChanged += (s,e) => populate();
            populate();
            AddActionButton("Seçili uygulamaları yükle", async () =>
            {
                var selected = list.SelectedItems.Cast<ListBoxItem>().Select(i => i.Tag as PackageEntry).Where(p => p != null).ToList();
                if (selected.Count == 0) { MessageBox.Show("Önce bir veya daha fazla uygulama seç."); return; }
                if (MessageBox.Show(selected.Count + " uygulama WinGet ile kurulacak. Devam?", "Uygulama kurulumu", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                foreach (var package in selected)
                {
                    PageSubtitle.Text = "Yükleniyor: " + package.Name;
                    try { string output = await _packageManager.InstallAsync(package.Id); _appLog.Write("INFO","WinGet install "+package.Id+"\n"+output); }
                    catch (Exception ex) { _appLog.Write("ERROR","WinGet install "+package.Id+": "+ex.Message); MessageBox.Show(package.Name+" yüklenemedi: "+ex.Message); }
                }
                PageSubtitle.Text = "Seçilen uygulama kurulumları tamamlandı.";
                MessageBox.Show("Kurulum kuyruğu tamamlandı. Ayrıntılar Sistem Günlükleri bölümüne kaydedildi.");
            });
            AddActionButton("Tüm uygulamaları güncelle", async () =>
            {
                if (MessageBox.Show("WinGet destekli yüklü uygulamalar yükseltilecek. Devam?", "Toplu güncelleme", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                try { PageSubtitle.Text = "Uygulamalar güncelleniyor..."; string output = await _packageManager.UpgradeAllAsync(); _appLog.Write("INFO","WinGet upgrade all\n"+output); MessageBox.Show(output,"WinGet güncelleme sonucu"); }
                catch (Exception ex) { MessageBox.Show(ex.Message,"WinGet"); }
                PageSubtitle.Text = "Uygulama yöneticisi";
            });
            AddActionButton("WinGet kaynaklarını görüntüle", () => Launch("cmd.exe", "/k winget source list"));
        }


        private void BuildTweaksPage()
        {
            AddInfoCard("Windows Tweaks • Geri alınabilir ayarlar", "Bu sayfadaki ilk tweak oyun yakalama ayarlarını yönetir. İlk değişiklikten önce mevcut kayıt defteri değerleri yedeklenir. Geri yükleme yedeği NEXORA'nın yerel uygulama verilerinde tutulur.");
            try { AddInfoCard("Mevcut oyun yakalama değerleri", _tweaks.GetStatus()); }
            catch (Exception ex) { AddInfoCard("Ayarlar okunamadı", ex.Message); }
            AddActionButton("Oyun yakalama / Game DVR kapat", () =>
            {
                if (MessageBox.Show("Windows Game DVR kayıt anahtarları kapatılacak. Xbox Game Bar kayıt özelliği etkilenebilir. Önceki değerler yedeklenecek. Devam?", "Tweak uygula", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                try { _tweaks.DisableGameCapture(); _appLog.Write("INFO", "Applied tweak: Disable Game DVR"); MessageBox.Show("Ayar uygulandı. Gerekirse oturumu kapatıp aç."); RefreshCurrentPage(); }
                catch (Exception ex) { MessageBox.Show("Tweak uygulanamadı: " + ex.Message); }
            });
            AddActionButton("Oyun yakalama / Game DVR aç", () =>
            {
                if (MessageBox.Show("Game DVR kayıt anahtarları etkinleştirilecek. Devam?", "Tweak uygula", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                try { _tweaks.EnableGameCapture(); _appLog.Write("INFO", "Applied tweak: Enable Game DVR"); MessageBox.Show("Ayar uygulandı."); RefreshCurrentPage(); }
                catch (Exception ex) { MessageBox.Show("Tweak uygulanamadı: " + ex.Message); }
            });
            AddActionButton("Önceki tweak ayarlarını geri yükle", () =>
            {
                if (!_tweaks.HasBackup) { MessageBox.Show("Geri yüklenecek yedek bulunamadı."); return; }
                if (MessageBox.Show("NEXORA'nın ilk değişiklik öncesi aldığı kayıt defteri yedeği geri yüklenecek. Devam?", "Geri yükle", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                try { _tweaks.RestoreBackup(); _appLog.Write("INFO", "Restored tweak backup"); MessageBox.Show("Önceki değerler geri yüklendi."); RefreshCurrentPage(); }
                catch (Exception ex) { MessageBox.Show("Geri yükleme başarısız: " + ex.Message); }
            });
            AddInfoCard("Diğer Windows ayarları", "Aşağıdaki araçlar değişiklik yapmadan ilgili Windows ayar ekranlarını açar.");
            AddActionButton("Oyun Modu", () => Launch("ms-settings:gaming-gamemode"));
            AddActionButton("Grafik performansı tercihleri", () => Launch("ms-settings:display-advancedgraphics"));
            AddActionButton("Görsel efektler / performans seçenekleri", () => Launch("SystemPropertiesPerformance.exe"));
            AddActionButton("Güç planları", () => Launch("powercfg.cpl"));
            AddActionButton("Başlangıç uygulamalarını yönet", () => Launch("ms-settings:startupapps"));
            AddActionButton("Gizlilik ayarları", () => Launch("ms-settings:privacy"));
            AddActionButton("Kişiselleştirme", () => Launch("ms-settings:personalization"));
        }

        private void BuildRepairPage()
        {
            AddInfoCard("Windows Onarım Merkezi", "Windows'un resmi sistem onarım komutlarını çalıştır. Bazı işlemler yönetici izni ister ve uzun sürebilir. Komut penceresi açılır; işlem sürerken kapatma.");
            AddActionButton("SFC • Sistem dosyalarını tara", () => Launch("cmd.exe", "/k sfc /scannow"));
            AddActionButton("DISM • Bileşen deposu sağlık kontrolü", () => Launch("cmd.exe", "/k DISM /Online /Cleanup-Image /CheckHealth"));
            AddActionButton("DISM • Bileşen deposunu onar", () =>
            {
                if (MessageBox.Show("DISM çevrimiçi Windows imajını onarmayı deneyecek. İnternet bağlantısı ve yönetici izni gerekebilir. Devam?", "Windows onarımı", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    Launch("cmd.exe", "/k DISM /Online /Cleanup-Image /RestoreHealth");
            });
            AddActionButton("Disk • Çevrimiçi tarama (C:)", () => Launch("cmd.exe", "/k chkdsk C: /scan"));
            AddActionButton("Windows Update sorun giderme", () => Launch("ms-settings:troubleshoot"));
            AddActionButton("Windows Update ayarları", () => Launch("ms-settings:windowsupdate"));
            AddActionButton("Güncelleme geçmişi", () => Launch("ms-settings:windowsupdate-history"));
        }

        private void BuildFeaturesPage()
        {
            AddInfoCard("Windows Özellikleri", "Windows isteğe bağlı özelliklerini yerleşik Windows penceresinden yönet. Değişiklikler Windows tarafından uygulanır; bazıları yeniden başlatma gerektirir.");
            AddActionButton("Windows özelliklerini aç veya kapat", () => Launch("optionalfeatures.exe"));
            AddActionButton("Windows Sandbox / sanallaştırma ayarları", () => Launch("appwiz.cpl"));
            AddActionButton("Geliştirici ayarları", () => Launch("ms-settings:developers"));
            AddActionButton("Windows Güvenliği", () => Launch("windowsdefender:"));
            AddInfoCard("Not", "NEXORA Windows Defender, güvenlik duvarı veya Windows Update korumalarını kapatmaz. İleri seviye özellik değişikliklerinde Windows'un onay ekranı kullanılır.");
        }


        private void BuildPowerPage()
        {
            AddInfoCard("Güç Planı Yöneticisi", "Windows güç planlarını görüntüle ve değiştir. Yüksek performans planı daha fazla güç tüketebilir ve her bilgisayarda aynı sonucu vermez.");
            try
            {
                AddInfoCard("Etkin güç planı", _powerPlans.GetActivePlan());
                AddInfoCard("Mevcut güç planları", _powerPlans.GetPlans());
            }
            catch (Exception ex) { AddInfoCard("Güç planları okunamadı", ex.Message); }
            foreach (string plan in new[] { "Balanced", "High performance", "Power saver" })
            {
                string selected = plan;
                AddActionButton(selected + " planını etkinleştir", () =>
                {
                    string warning = selected == "High performance" ? "Bu plan güç tüketimini ve sıcaklığı artırabilir. Devam?" : selected == "Power saver" ? "Bu plan performansı sınırlayabilir. Devam?" : "Dengeli güç planı etkinleştirilsin mi?";
                    if (MessageBox.Show(warning, "Güç planı", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                    try { string result = _powerPlans.Activate(selected); _appLog.Write("INFO", "Power plan activated: " + selected + "\n" + result); MessageBox.Show(result, "NEXORA"); RefreshCurrentPage(); }
                    catch (Exception ex) { MessageBox.Show(ex.Message, "Güç planı", MessageBoxButton.OK, MessageBoxImage.Warning); }
                });
            }
            AddActionButton("Gelişmiş güç seçenekleri", () => Launch("powercfg.cpl"));
        }

        private void BuildUpdatesPage()
        {
            AddInfoCard("Windows Update Merkezi", "Windows güncellemelerini Windows'un resmi arayüzünden yönet. NEXORA güncelleme servislerini kapatmaz veya güncellemeleri zorla engellemez.");
            AddActionButton("Güncellemeleri denetle", () => Launch("ms-settings:windowsupdate"));
            AddActionButton("Güncelleme geçmişi", () => Launch("ms-settings:windowsupdate-history"));
            AddActionButton("Gelişmiş güncelleme seçenekleri", () => Launch("ms-settings:windowsupdate-options"));
            AddActionButton("İsteğe bağlı güncellemeler / sürücüler", () => Launch("ms-settings:windowsupdate-optionalupdates"));
            AddActionButton("Etkin saatler", () => Launch("ms-settings:windowsupdate-activehours"));
            AddInfoCard("Sürücü güvenliği", "Sürücüleri yalnızca üretici veya Windows Update kaynaklarından yükle. NEXORA şu anda sürücü paketi indirmez ve sürücüleri otomatik değiştirmez.");
        }

        private void BuildConfigPage()
        {
            AddInfoCard("Sistem Yapılandırması", "Windows ayarlarını kategori kategori aç. Değişikliklerin uygulanması Windows tarafından yapılır.");
            AddActionButton("Ekran ve çözünürlük", () => Launch("ms-settings:display"));
            AddActionButton("Ses ayarları", () => Launch("ms-settings:sound"));
            AddActionButton("Bluetooth ve cihazlar", () => Launch("ms-settings:bluetooth"));
            AddActionButton("Ağ ve internet", () => Launch("ms-settings:network"));
            AddActionButton("Varsayılan uygulamalar", () => Launch("ms-settings:defaultapps"));
            AddActionButton("Yüklü uygulamalar", () => Launch("ms-settings:appsfeatures"));
            AddActionButton("Kullanıcı hesapları", () => Launch("ms-settings:yourinfo"));
            AddActionButton("Tarih ve saat", () => Launch("ms-settings:dateandtime"));
            AddActionButton("Dil ve bölge", () => Launch("ms-settings:regionlanguage"));
            AddActionButton("Erişilebilirlik", () => Launch("ms-settings:easeofaccess"));
            AddActionButton("Depolama", () => Launch("ms-settings:storagesense"));
            AddActionButton("Kurtarma seçenekleri", () => Launch("ms-settings:recovery"));
        }


        private void BuildDebloatPage()
        {
            AddInfoCard("Debloat Manager • Uygulama kaldırma", "WinGet kataloğundan seçtiğin masaüstü uygulamalarını kaldırır. Windows sistem bileşenleri, Defender ve kritik uygulamalar bu listede bulunmaz. Kaldırma işlemi geri alınamayabilir.");
            if (!_packageManager.IsWingetAvailable())
            {
                AddInfoCard("WinGet gerekli", "Debloat işlemleri için Microsoft App Installer / WinGet gerekir.");
                AddActionButton("App Installer'ı aç", () => Launch("ms-windows-store://pdp/?ProductId=9NBLGGH4NNS1"));
                return;
            }
            var candidates = new List<PackageEntry>
            {
                new PackageEntry{Name="Xbox Game Bar",Id="Microsoft.XboxGamingOverlay",Category="Oyun yardımcı aracı"},
                new PackageEntry{Name="Microsoft Teams (kişisel)",Id="Microsoft.Teams",Category="İletişim"},
                new PackageEntry{Name="Microsoft Clipchamp",Id="Clipchamp.Clipchamp",Category="Video"},
                new PackageEntry{Name="Spotify",Id="Spotify.Spotify",Category="Medya"},
                new PackageEntry{Name="Discord",Id="Discord.Discord",Category="İletişim"},
                new PackageEntry{Name="Microsoft OneDrive",Id="Microsoft.OneDrive",Category="Bulut"},
                new PackageEntry{Name="Microsoft To Do",Id="9NBLGGH5R558",Category="Üretkenlik"}
            };
            var list = new ListBox { Height = 300, SelectionMode = SelectionMode.Extended, Background = (Brush)FindResource("PanelBrush"), Foreground = (Brush)FindResource("TextBrush"), BorderThickness = new Thickness(0) };
            foreach (var app in candidates) list.Items.Add(new ListBoxItem { Content = app.Name + "  •  " + app.Category + "  |  " + app.Id, Tag = app, Padding = new Thickness(8) });
            PageContent.Children.Add(list);
            AddActionButton("Seçili uygulamaları kaldır", async () =>
            {
                var selected = list.SelectedItems.Cast<ListBoxItem>().Select(i => i.Tag as PackageEntry).Where(p => p != null).ToList();
                if (selected.Count == 0) { MessageBox.Show("Önce kaldırılacak uygulamaları seç."); return; }
                if (MessageBox.Show(selected.Count + " uygulama WinGet ile kaldırılmayı deneyecek. Devam etmeden önce uygulama verilerini yedekle. Devam?", "Debloat onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                foreach (var app in selected)
                {
                    try { PageSubtitle.Text = "Kaldırılıyor: " + app.Name; var output = await _packageManager.UninstallAsync(app.Id); _appLog.Write("INFO", "WinGet uninstall " + app.Id + "\n" + output); }
                    catch (Exception ex) { _appLog.Write("ERROR", "Uninstall " + app.Id + ": " + ex.Message); MessageBox.Show(app.Name + " kaldırılamadı: " + ex.Message); }
                }
                PageSubtitle.Text = "Debloat işlemleri tamamlandı.";
                MessageBox.Show("Kaldırma denemeleri tamamlandı. Sonuçları Sistem Günlükleri bölümünden kontrol et.");
            });
            AddActionButton("Yüklü uygulamalar sayfasını aç", () => Launch("ms-settings:appsfeatures"));
            AddInfoCard("Güvenli kaldırma ilkesi", "Windows bileşenleri, Store altyapısı, WebView, .NET, sürücüler ve güvenlik yazılımları otomatik kaldırılmaz. Listede olmayan uygulamaları Windows'un kendi kaldırma ekranından yönet.");
        }

        private void BuildProcessesPage()
        {
            var entries = _processService.GetProcesses();
            AddInfoCard("Canlı işlem yöneticisi", "İşlemleri ada veya PID'ye göre filtrele. Bellek kullanımı yüksekten düşüğe sıralanır. Kritik Windows işlemleri korunur.");
            AddInfoCard("Toplam işlem: " + entries.Count, "Liste anlık görüntüdür; yenile düğmesiyle tekrar tarayabilirsin.");
            var search = new TextBox { Height = 36, Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 10), Background = (Brush)FindResource("PanelBrush"), Foreground = (Brush)FindResource("TextBrush"), BorderThickness = new Thickness(0), ToolTip = "İşlem adı veya PID ara" };
            PageContent.Children.Add(search);
            var list = new ListBox { Height = 390, Background = (Brush)FindResource("PanelBrush"), Foreground = (Brush)FindResource("TextBrush"), BorderThickness = new Thickness(0) };
            PageContent.Children.Add(list);
            Action populate = () =>
            {
                list.Items.Clear();
                string term = search.Text.Trim();
                foreach (var p in entries.Where(x => string.IsNullOrEmpty(term) || x.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 || x.Id.ToString().Contains(term)).Take(250))
                    list.Items.Add(new ListBoxItem { Content = p.Name + "  |  PID " + p.Id + "  |  RAM " + p.MemoryMb + " MB" + (string.IsNullOrWhiteSpace(p.WindowTitle) ? "" : "  |  " + p.WindowTitle), Tag = p.Id, Padding = new Thickness(8) });
            };
            search.TextChanged += (s, e) => populate();
            populate();
            AddActionButton("Seçili işlemi sonlandır", () =>
            {
                var selected = list.SelectedItem as ListBoxItem;
                if (selected == null) { MessageBox.Show("Önce listeden bir işlem seç."); return; }
                int id = Convert.ToInt32(selected.Tag);
                if (MessageBox.Show("Seçilen işlem sonlandırılsın mı? Kaydedilmemiş çalışmalar kaybolabilir.", "İşlem sonlandırma", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                try { _processService.EndProcess(id); _appLog.Write("INFO", "Process terminated: PID " + id); RefreshCurrentPage(); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "NEXORA", MessageBoxButton.OK, MessageBoxImage.Warning); }
            });
            AddActionButton("Seçili işlemin dosya konumunu aç", () =>
            {
                var selected = list.SelectedItem as ListBoxItem;
                if (selected == null) { MessageBox.Show("Önce bir işlem seç."); return; }
                try { using (var p = Process.GetProcessById(Convert.ToInt32(selected.Tag))) { string path = p.MainModule.FileName; Launch("explorer.exe", "/select,\"" + path + "\""); } }
                catch (Exception ex) { MessageBox.Show("Dosya konumu okunamadı: " + ex.Message); }
            });
            AddActionButton("Seçili PID'yi kopyala", () =>
            {
                var selected = list.SelectedItem as ListBoxItem;
                if (selected == null) { MessageBox.Show("Önce bir işlem seç."); return; }
                Clipboard.SetText(Convert.ToString(selected.Tag));
            });
            AddActionButton("Listeyi yenile", RefreshCurrentPage);
        }

        private void RefreshCurrentPage()
        {
            var button = new Button { Tag = _currentPageKey };
            Navigate_Click(button, new RoutedEventArgs());
        }

        private void BuildStartupPage()
        {
            AddInfoCard("Başlangıç uygulamaları", "Kullanıcı Run girdilerini NEXORA içinden devre dışı bırakıp geri yükleyebilirsin. Bilgisayar genelindeki girdiler salt okunurdur. Devre dışı bırakma girdiyi silmez; NEXORA kayıt alanına taşır.");
            var entries = _startupService.GetAllEntries();
            var list = new ListBox { Height = 330, Background = (Brush)FindResource("PanelBrush"), Foreground = (Brush)FindResource("TextBrush"), BorderThickness = new Thickness(0) };
            foreach (var item in entries)
                list.Items.Add(new ListBoxItem { Content = item.Name + "  |  " + item.Location + "  |  " + item.Command, Tag = item, Padding = new Thickness(7) });
            PageContent.Children.Add(list);
            AddActionButton("Seçili kullanıcı girdisini devre dışı bırak", () =>
            {
                var selected = list.SelectedItem as ListBoxItem;
                var entry = selected == null ? null : selected.Tag as StartupEntry;
                if (entry == null) { MessageBox.Show("Önce bir başlangıç girdisi seç."); return; }
                if (!string.Equals(entry.Location, "Kullanıcı", StringComparison.OrdinalIgnoreCase))
                { MessageBox.Show("Bilgisayar genelindeki girdiler bu ekrandan değiştirilemez."); return; }
                if (MessageBox.Show(entry.Name + " başlangıçtan çıkarılacak. İstediğinde geri yükleyebilirsin. Devam?", "Başlangıç yönetimi", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                try { _startupService.DisableCurrentUserEntry(entry.Name); _appLog.Write("INFO", "Disabled startup entry: " + entry.Name); RefreshCurrentPage(); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Başlangıç yönetimi"); }
            });
            var disabled = _startupService.GetDisabledEntries();
            var restoreList = new ListBox { Height = 130, Background = (Brush)FindResource("PanelBrush"), Foreground = (Brush)FindResource("TextBrush"), BorderThickness = new Thickness(0) };
            foreach (var name in disabled) restoreList.Items.Add(name);
            AddInfoCard("NEXORA tarafından devre dışı bırakılanlar", disabled.Count == 0 ? "Geri yüklenecek kayıt bulunmuyor." : disabled.Count + " kayıt");
            PageContent.Children.Add(restoreList);
            AddActionButton("Seçili girdiyi geri yükle", () =>
            {
                var name = restoreList.SelectedItem as string;
                if (string.IsNullOrWhiteSpace(name)) { MessageBox.Show("Önce geri yüklenecek girdiyi seç."); return; }
                try { _startupService.RestoreDisabledEntry(name); _appLog.Write("INFO", "Restored startup entry: " + name); RefreshCurrentPage(); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Başlangıç yönetimi"); }
            });
            AddActionButton("Başlangıç Uygulamaları klasörünü aç", () => Launch("shell:startup"));
            AddActionButton("Görev Yöneticisi başlangıç sekmesini aç", () => Launch("taskmgr.exe"));
        }

        private void BuildServicesPage()
        {
            AddInfoCard("Windows hizmetleri", "Hizmetler listelenir. Durdurma işlemi yalnızca kullanıcı onayıyla yapılır. Defender, güvenlik duvarı, Windows Update ve kritik Windows hizmetleri koruma listesindedir.");
            var list = new ListBox { Height = 330, Background = (Brush)FindResource("PanelBrush"), Foreground = (Brush)FindResource("TextBrush"), BorderThickness = new Thickness(0) };
            foreach (var s in _windowsServices.GetServices())
                list.Items.Add(new ListBoxItem { Content = s.DisplayName + "  |  " + s.Status + "  |  " + s.Name, Tag = s.Name, Padding = new Thickness(7) });
            PageContent.Children.Add(list);
            AddActionButton("Seçili hizmeti durdur", () =>
            {
                var selected = list.SelectedItem as ListBoxItem;
                if (selected == null) { MessageBox.Show("Önce bir hizmet seç."); return; }
                string service = Convert.ToString(selected.Tag);
                if (MessageBox.Show("“" + service + "” hizmeti durdurulsun mu? Bazı uygulamalar etkilenebilir.", "Hizmet yönetimi", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                try { _windowsServices.StopService(service); MessageBox.Show("Hizmet durduruldu."); RefreshCurrentPage(); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Hizmet durdurulamadı", MessageBoxButton.OK, MessageBoxImage.Warning); }
            });
            AddActionButton("Hizmet listesini yenile", RefreshCurrentPage);
        }

        private void BuildStoragePage()
        {
            long bytes = 0;
            try { bytes = _storageService.GetTemporaryFileBytes(); } catch { }
            AddInfoCard("Geçici dosyalar", "Kullanıcı TEMP klasörü: " + _storageService.TempPath + "\nYaklaşık taranan boyut: " + (bytes / 1048576d).ToString("0.0") + " MB\nKilitli veya kullanımda olan dosyalar atlanır.");
            AddActionButton("Geçici dosyaları temizle", () =>
            {
                if (MessageBox.Show("Yalnızca Windows kullanıcı TEMP klasöründeki silinebilen dosyalar temizlenecek. Devam edilsin mi?", "Depolama temizliği", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                int count = _storageService.CleanTemporaryFiles();
                _appLog.Write("INFO", "Temporary cleanup completed. Deleted files: " + count);
                MessageBox.Show("Temizlenen dosya sayısı: " + count);
                RefreshCurrentPage();
            });
            AddActionButton("Depolama ayarlarını aç", () => Launch("ms-settings:storagesense"));
            AddActionButton("Geri Dönüşüm Kutusu'nu aç", () => Launch("explorer.exe", "shell:RecycleBinFolder"));
            AddActionButton("İndirilenler klasörünü aç", () => Launch("explorer.exe", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")));
            AddActionButton("Disk Temizleme aracını aç", () => Launch("cleanmgr.exe"));
        }

        private void BuildNetworkPage()
        {
            AddInfoCard("Ağ araçları", "Bu araçlar bağlantı bilgilerini görüntüler. DNS önbelleği temizliği dışında ağ yapılandırması değiştirilmez.");
            AddActionButton("IP yapılandırmasını görüntüle", () =>
            {
                try { MessageBox.Show(_networkService.GetIpConfiguration(), "IP yapılandırması", MessageBoxButton.OK, MessageBoxImage.Information); }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            });
            AddActionButton("DNS önbelleğini temizle", () =>
            {
                if (MessageBox.Show("Windows DNS önbelleği temizlensin mi?", "Ağ aracı", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                try { _networkService.FlushDns(); _appLog.Write("INFO", "DNS cache flushed."); MessageBox.Show("DNS önbelleği temizlendi."); }
                catch (Exception ex) { MessageBox.Show("İşlem başarısız: " + ex.Message); }
            });
            AddActionButton("Ağ ve İnternet ayarlarını aç", () => Launch("ms-settings:network-status"));
            AddActionButton("Ağ bağlantılarını aç", () => Launch("ncpa.cpl"));
            AddActionButton("Bağlantı testi (1.1.1.1)", () =>
            {
                try { var psi = new ProcessStartInfo { FileName = "ping.exe", Arguments = "-n 4 1.1.1.1", UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true }; using (var p = Process.Start(psi)) { string output = p.StandardOutput.ReadToEnd(); p.WaitForExit(10000); MessageBox.Show(output, "Bağlantı testi"); } }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            });
            AddActionButton("Proxy ayarlarını aç", () => Launch("ms-settings:network-proxy"));
        }

        private void BuildGamingPage()
        {
            AddInfoCard("Gaming Center • Oyun oturumu", "Çalışan oyun süreçlerini algıla ve yalnızca seçtiğin oyunun işlem önceliğini yönet. Yüksek öncelik bazı sistemlerde diğer uygulamaların tepkisini azaltabilir; Gerçek Zamanlı (Realtime) kullanılmaz.");
            var games = _gameOptimizer.GetRunningGames();
            AddInfoCard("Algılanan oyun sayısı: " + games.Count, games.Count == 0 ? "Şu anda desteklenen oyunlardan biri çalışmıyor." : "İşlem önceliği: Normal veya High. Değişiklik yalnızca seçilen çalışan oyun süreci için geçerlidir.");
            var list = new ListBox { Height = 220, Background = (Brush)FindResource("PanelBrush"), Foreground = (Brush)FindResource("TextBrush"), BorderThickness = new Thickness(0) };
            foreach (var game in games)
                list.Items.Add(new ListBoxItem { Content = game.Name + "  |  PID " + game.Id + "  |  Öncelik: " + game.Priority, Tag = game.Id, Padding = new Thickness(8) });
            PageContent.Children.Add(list);
            AddActionButton("Seçili oyuna yüksek öncelik ver", () =>
            {
                var selected = list.SelectedItem as ListBoxItem;
                if (selected == null) { MessageBox.Show("Önce çalışan bir oyun seç."); return; }
                if (MessageBox.Show("Seçilen oyun süreci High önceliğe alınacak. Bu FPS artışını garanti etmez. Devam?", "Oyun profili", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                try { _gameOptimizer.SetHighPriority(Convert.ToInt32(selected.Tag)); _appLog.Write("INFO", "Game process priority set to High: PID " + selected.Tag); MessageBox.Show("İşlem önceliği High olarak ayarlandı."); RefreshCurrentPage(); }
                catch (Exception ex) { MessageBox.Show("Öncelik değiştirilemedi: " + ex.Message, "Gaming Center", MessageBoxButton.OK, MessageBoxImage.Warning); }
            });
            AddActionButton("Seçili oyunun önceliğini normale döndür", () =>
            {
                var selected = list.SelectedItem as ListBoxItem;
                if (selected == null) { MessageBox.Show("Önce çalışan bir oyun seç."); return; }
                try { _gameOptimizer.RestoreNormalPriority(Convert.ToInt32(selected.Tag)); _appLog.Write("INFO", "Game process priority restored: PID " + selected.Tag); MessageBox.Show("İşlem önceliği Normal yapıldı."); RefreshCurrentPage(); }
                catch (Exception ex) { MessageBox.Show("Öncelik geri alınamadı: " + ex.Message, "Gaming Center", MessageBoxButton.OK, MessageBoxImage.Warning); }
            });
            AddActionButton("Oyunları yeniden tara", RefreshCurrentPage);
            AddActionButton("Windows Oyun Modu ayarlarını aç", () => Launch("ms-settings:gaming-gamemode"));
            AddActionButton("Xbox Game Bar ayarlarını aç", () => Launch("ms-settings:gaming-gamebar"));
            AddActionButton("Grafik tercihleri ayarlarını aç", () => Launch("ms-settings:display-advancedgraphics"));
            AddActionButton("Oyun yakalama ayarlarını aç", () => Launch("ms-settings:gaming-captures"));
        }

        private void BuildQuickBoostPage()
        {
            AddInfoCard("Quick Boost • Güvenli araçlar", "Bu ilk sürümde tek tuşla servis kapatma veya kayıt defteri değişikliği yapılmaz. Gerçek ve geri döndürülebilir araçlar sunulur.");
            AddActionButton("Geçici dosyaları tara", () => MessageBox.Show("TEMP klasörü: " + _storageService.TempPath + "\nYaklaşık boyut: " + (_storageService.GetTemporaryFileBytes() / 1048576d).ToString("0.0") + " MB", "Quick Boost", MessageBoxButton.OK, MessageBoxImage.Information));
            AddActionButton("Geçici dosyaları güvenli temizle", () => { if (MessageBox.Show("Yalnızca kullanıcı TEMP klasöründeki kullanılmayan dosyalar temizlenecek. Devam?", "Quick Boost", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) { int n = _storageService.CleanTemporaryFiles(); _appLog.Write("INFO", "Quick Boost TEMP cleanup: " + n); MessageBox.Show("Temizlenen dosya: " + n); RefreshCurrentPage(); } });
            AddActionButton("Güç ve pil ayarlarını aç", () => Launch("ms-settings:powersleep"));
            AddActionButton("Görsel efekt ayarlarını aç", () => Launch("SystemPropertiesPerformance.exe"));
        }

        private void BuildBenchmarkPage()
        {
            AddInfoCard("Sistem tanılama", "Donanım bilgilerini ve sistem yapılandırmasını toplar. Bu ekran henüz FPS ölçümü veya sentetik benchmark puanı üretmez.");
            AddActionButton("Tanılama raporunu göster", () => MessageBox.Show(_diagnostics.GetSummary(), "NEXORA Sistem Raporu", MessageBoxButton.OK, MessageBoxImage.Information));
            AddActionButton("Windows Performans İzleyicisi", () => Launch("perfmon.exe"));
            AddActionButton("DirectX tanı aracı", () => Launch("dxdiag.exe"));
        }

        private void BuildLogsPage()
        {
            AddInfoCard("NEXORA günlükleri", "Uygulama içindeki araç işlemleri ve hatalar yerel olarak kaydedilir.\nDosya: " + _appLog.LogPath);
            var box = new TextBox { Text = _appLog.ReadAll(), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 300, Background = (Brush)FindResource("PanelBrush"), Foreground = (Brush)FindResource("TextBrush"), BorderThickness = new Thickness(0), Padding = new Thickness(12) };
            PageContent.Children.Add(box);
            AddActionButton("Günlükleri yenile", RefreshCurrentPage);
            AddActionButton("Günlük klasörünü aç", () => Launch(Path.GetDirectoryName(_appLog.LogPath)));
        }

        private void BuildRestorePage()
        {
            AddInfoCard("Sistem geri yükleme", "Windows geri yükleme noktaları sistem değişikliklerini geri almayı kolaylaştırabilir. NEXORA henüz otomatik geri yükleme noktası oluşturmaz; Windows'un yerleşik koruma ekranını açar.");
            AddActionButton("Sistem Koruması ayarlarını aç", () => Launch("SystemPropertiesProtection.exe"));
            AddActionButton("Sistem geri yüklemeyi başlat", () => Launch("rstrui.exe"));
        }

        private void BuildHardwarePage()
        {
            AddInfoCard("Canlı donanım özeti", "Dashboard üzerindeki CPU, RAM, GPU modeli ve sistem çalışma süresi bilgileri 5 saniyede bir yenilenir. Bazı GPU sıcaklığı, fan hızı ve güç sensörleri donanıma veya sürücüye bağlı olarak kullanılamayabilir.");
            AddActionButton("Şimdi yenile", async () => await RefreshSystemInfoAsync());
            foreach (string detail in _gpuDiagnostics.GetAdapters()) AddInfoCard("GPU / Ekran bağdaştırıcısı", detail);
            AddInfoCard("Sensör doğruluğu", "Bu sürüm GPU sürücüsü ve bağdaştırıcı bilgilerini WMI üzerinden okur. GPU sıcaklığı, fan devri ve gerçek zamanlı VRAM kullanımı her sürücü tarafından sunulmadığı için henüz desteklenmiyor; sahte değer gösterilmez.");
            AddActionButton("Aygıt Yöneticisi • Görüntü bağdaştırıcıları", () => Launch("devmgmt.msc"));
            AddActionButton("Windows gelişmiş ekran ayarları", () => Launch("ms-settings:display-advanced"));
        }

        private void BuildWindowsToolsPage()
        {
            AddInfoCard("Windows yerleşik araçları", "Aşağıdaki kısayollar Windows'un kendi araçlarını açar. NEXORA sistem ayarlarını değiştirmez.");
            AddActionButton("Görev Yöneticisi", () => Launch("taskmgr.exe"));
            AddActionButton("Kaynak İzleyicisi", () => Launch("resmon.exe"));
            AddActionButton("Aygıt Yöneticisi", () => Launch("devmgmt.msc"));
            AddActionButton("Disk Temizleme", () => Launch("cleanmgr.exe"));
            AddActionButton("Hizmetler", () => Launch("services.msc"));
            AddActionButton("Olay Görüntüleyicisi", () => Launch("eventvwr.msc"));
            AddActionButton("Sistem Yapılandırması", () => Launch("msconfig.exe"));
            AddActionButton("Görev Zamanlayıcı", () => Launch("taskschd.msc"));
            AddActionButton("Güç seçenekleri", () => Launch("powercfg.cpl"));
            AddActionButton("Program kaldırma", () => Launch("appwiz.cpl"));
            AddActionButton("Sistem bilgisi", () => Launch("msinfo32.exe"));
            AddActionButton("Windows Güvenliği", () => Launch("windowsdefender:"));
            AddActionButton("Windows Ayarları", () => Launch("ms-settings:"));
        }

        private void BuildSettingsPage()
        {
            AddInfoCard("Genel ayarlar", "Bu sürümde yenileme aralığını değiştirebilirsin. Uygulama kapatıldığında izleme zamanlayıcısı durdurulur.");
            AddActionButton("Yenileme: 3 saniye", () => SetRefreshInterval(3));
            AddActionButton("Yenileme: 5 saniye (varsayılan)", () => SetRefreshInterval(5));
            AddActionButton("Yenileme: 10 saniye", () => SetRefreshInterval(10));
            AddInfoCard("Uygulama güncellemeleri", "NEXORA yeni sürümleri GitHub üzerinden kontrol eder. Güncelleme bulunduğunda paketi indirir, uygulamayı kapatıp dosyaları yeniler ve yeni sürümü yeniden başlatır.");
            AddActionButton("Güncellemeleri kontrol et", async () => await CheckForUpdatesAsync(true));
            AddInfoCard("Güvenlik", "NEXORA başlangıçta yönetici izni istemez. Yönetici yetkisi gerektiren işlemler ilgili özellik tamamlandığında yalnızca gerektiğinde istenecek.");
        }


        private async Task CheckForUpdatesAsync(bool userInitiated)
        {
            try
            {
                var update = await _updateService.GetAvailableUpdateAsync();
                if (update == null)
                {
                    if (userInitiated)
                        MessageBox.Show("Şu anda en güncel sürümü kullanıyorsun.\nSürüm: " + _updateService.CurrentVersion, "NEXORA Güncelleme", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var choice = MessageBox.Show("Yeni NEXORA sürümü bulundu: " + update.Version + "\nMevcut sürüm: " + _updateService.CurrentVersion + "\n\nGüncelleme şimdi indirilip otomatik olarak kurulsun mu?", "NEXORA Güncelleme", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (choice != MessageBoxResult.Yes) return;

                await _updateService.DownloadAndApplyAsync(update);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                if (userInitiated)
                    MessageBox.Show("Güncelleme tamamlanamadı.\n\n" + ex.Message, "NEXORA Güncelleme", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SetRefreshInterval(int seconds)
        {
            _refreshTimer.Interval = TimeSpan.FromSeconds(seconds);
            PageSubtitle.Text = "Sistem bilgileri " + seconds + " saniyede bir yenilenecek.";
        }

        private void Launch(string command, string arguments)
        {
            try { Process.Start(new ProcessStartInfo { FileName = command, Arguments = arguments, UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show("Araç açılamadı: " + ex.Message, "NEXORA Performance", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void Launch(string command)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = command, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Araç açılamadı: " + ex.Message, "NEXORA Performance", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void AddInfoCard(string title, string message)
        {
            var accent = new Border
            {
                Width = 4,
                CornerRadius = new CornerRadius(3),
                Background = (Brush)FindResource("AccentGradient"),
                Margin = new Thickness(0, 2, 15, 2)
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("TextBrush") });
            stack.Children.Add(new TextBlock { Text = message, FontSize = 13, LineHeight = 20, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("MutedBrush"), Margin = new Thickness(0, 9, 0, 0) });
            var content = new DockPanel();
            DockPanel.SetDock(accent, Dock.Left);
            content.Children.Add(accent);
            content.Children.Add(stack);
            var panel = new Border
            {
                Background = (Brush)FindResource("PanelGradient"),
                BorderBrush = new SolidColorBrush(Color.FromRgb(58, 42, 77)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(15),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 14),
                Child = content
            };
            PageContent.Children.Add(panel);
        }

        private void AddActionButton(string label, Action action)
        {
            var button = new Button
            {
                Content = "✦   " + label,
                Background = (Brush)FindResource("PanelLightBrush"),
                Foreground = (Brush)FindResource("TextBrush"),
                Padding = new Thickness(17, 13, 17, 13),
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                BorderBrush = new SolidColorBrush(Color.FromRgb(65, 47, 88)),
                BorderThickness = new Thickness(1)
            };
            button.Click += (s, e) => action();
            PageContent.Children.Add(button);
        }
    }
}
