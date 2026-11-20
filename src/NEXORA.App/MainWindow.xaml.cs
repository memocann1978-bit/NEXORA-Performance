using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NEXORA.App.Services;
namespace NEXORA.App
{
    public partial class MainWindow : Window
    {
        private readonly SystemInfoService _systemInfo = new SystemInfoService();
        private readonly DispatcherTimer _refreshTimer = new DispatcherTimer();
        public MainWindow()
        {
            InitializeComponent();
            Loaded += async (s, e) => await RefreshSystemInfoAsync();
            _refreshTimer.Interval = TimeSpan.FromSeconds(5);
            _refreshTimer.Tick += async (s, e) => await RefreshSystemInfoAsync();
            _refreshTimer.Start();
        }
        private async Task RefreshSystemInfoAsync()
        {
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
            catch { }
        }
        private void Navigate_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;
            var key = Convert.ToString(button.Tag);
            var titles = new System.Collections.Generic.Dictionary<string, string>
            {
                {"dashboard","Genel Bakış"},{"boost","Quick Boost"},{"gaming","Gaming Center"},{"hardware","Donanım İzleme"},
                {"processes","İşlem Yöneticisi"},{"startup","Başlangıç Uygulamaları"},{"windows","Windows Araçları"},{"settings","Ayarlar"}
            };
            PageTitle.Text = titles.ContainsKey(key) ? titles[key] : "NEXORA";
            if (key == "dashboard") { PageSubtitle.Text = "Sisteminin durumunu tek merkezden takip et."; return; }
            PageSubtitle.Text = "NEXORA Performance • Preview";
            PageContent.Children.Clear();
            var panel = new Border { Background = (System.Windows.Media.Brush)FindResource("PanelBrush"), CornerRadius = new CornerRadius(14), Padding = new Thickness(26), Margin = new Thickness(0,0,0,12) };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = titles.ContainsKey(key) ? titles[key] : "Modül", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = (System.Windows.Media.Brush)FindResource("TextBrush") });
            stack.Children.Add(new TextBlock { Text = key == "boost" ? "Güvenli optimizasyon profilleri hazırlanıyor. Bu önizleme sürümünde henüz sistem değişikliği uygulanmaz." : "Bu modül geliştirme yol haritasında. Henüz etkin olmayan özellikler tamamlanmış gibi gösterilmez.", FontSize = 13, TextWrapping = TextWrapping.Wrap, Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush"), Margin = new Thickness(0,10,0,0) });
            panel.Child = stack;
            PageContent.Children.Add(panel);
        }
    }
}
