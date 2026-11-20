using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        private readonly DispatcherTimer _refreshTimer = new DispatcherTimer();
        private readonly List<UIElement> _dashboardElements = new List<UIElement>();
        private bool _refreshInProgress;

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
            var titles = new Dictionary<string, string>
            {
                {"dashboard","Genel Bakış"},{"boost","Quick Boost"},{"gaming","Gaming Center"},
                {"hardware","Donanım İzleme"},{"processes","İşlem Yöneticisi"},
                {"startup","Başlangıç Uygulamaları"},{"windows","Windows Araçları"},{"settings","Ayarlar"}
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
            else if (key == "hardware") BuildHardwarePage();
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

        private void BuildHardwarePage()
        {
            AddInfoCard("Canlı donanım özeti", "Dashboard üzerindeki CPU, RAM, GPU modeli ve sistem çalışma süresi bilgileri 5 saniyede bir yenilenir. Bazı GPU sıcaklığı, fan hızı ve güç sensörleri donanıma veya sürücüye bağlı olarak kullanılamayabilir.");
            AddActionButton("Şimdi yenile", async () => await RefreshSystemInfoAsync());
            AddInfoCard("Sensör doğruluğu", "NEXORA desteklenmeyen sensörler için tahmini değer üretmez. Ayrıntılı sıcaklık ve fan izleme sonraki modüllerde eklenecek.");
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
            var panel = new Border
            {
                Background = (Brush)FindResource("PanelBrush"),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(22),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("TextBrush") });
            stack.Children.Add(new TextBlock { Text = message, FontSize = 13, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("MutedBrush"), Margin = new Thickness(0, 9, 0, 0) });
            panel.Child = stack;
            PageContent.Children.Add(panel);
        }

        private void AddActionButton(string label, Action action)
        {
            var button = new Button
            {
                Content = label,
                Background = (Brush)FindResource("PanelLightBrush"),
                Foreground = (Brush)FindResource("TextBrush"),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 9),
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            button.Click += (s, e) => action();
            PageContent.Children.Add(button);
        }
    }
}
