using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;
using WaveTools.Depend;
using static WaveTools.App;

namespace WaveTools.Views.SGViews
{
    public sealed partial class GameResourceQualitySelector : UserControl
    {
        private string _gamePath;
        private int _generation;
        private bool _applying;

        public GameResourceQualitySelector()
        {
            InitializeComponent();
            Loaded += (_, _) => _ = RefreshAsync();
            Unloaded += (_, _) => ++_generation;
        }

        public void SetGamePath(string gamePath)
        {
            if (!string.Equals(_gamePath, gamePath, StringComparison.OrdinalIgnoreCase))
            {
                _applying = true;
                QualityBox.ItemsSource = null;
                QualityBox.IsEnabled = false;
                QualityBox.PlaceholderText = "检测中";
                _applying = false;
            }
            _gamePath = gamePath;
            _ = RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            int generation = ++_generation;
            string path = _gamePath;
            bool hasPath = !string.IsNullOrWhiteSpace(path) && !path.Equals("Null", StringComparison.OrdinalIgnoreCase);
            if (!hasPath)
            {
                _applying = true;
                QualityBox.ItemsSource = null;
                QualityBox.IsEnabled = false;
                QualityBox.PlaceholderText = "未选择游戏";
                ToolTipService.SetToolTip(QualityBox, null);
                _applying = false;
                return;
            }
            try
            {
                var installation = await Task.Run(() => GameResourceQuality.Detect(path));
                if (generation != _generation) return;
                string selected = GameResourceQuality.Select(installation,
                    AppDataController.GetGameResourceQuality(path), AppDataController.GetGameParameter());
                _applying = true;
                var previous = (QualityBox.ItemsSource as System.Collections.Generic.IEnumerable<GameResourceQuality.Option>)?.ToList();
                if (previous == null || !previous.Select(option => option.Code).SequenceEqual(installation.Options.Select(option => option.Code)))
                    QualityBox.ItemsSource = installation.Options;
                QualityBox.SelectedItem = QualityBox.Items.OfType<GameResourceQuality.Option>().FirstOrDefault(option => option.Code == selected);
                QualityBox.IsEnabled = installation.Options.Count > 0;
                QualityBox.PlaceholderText = "未识别";
                ToolTipService.SetToolTip(QualityBox, installation.Error);
            }
            catch (Exception ex)
            {
                if (generation != _generation) return;
                QualityBox.ItemsSource = null;
                QualityBox.IsEnabled = false;
                QualityBox.PlaceholderText = "未识别";
                Logging.Write($"Refresh resource quality failed: {ex.Message}", 1);
            }
            finally { if (generation == _generation) _applying = false; }
        }

        private void QualityBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_applying || QualityBox.SelectedItem is not GameResourceQuality.Option option) return;
            try { AppDataController.SetGameResourceQuality(_gamePath, option.Code); }
            catch (Exception ex)
            {
                Logging.Write($"Save resource quality failed: {ex.Message}", 1);
                NotificationManager.RaiseNotification("画质选择保存失败", ex.Message, InfoBarSeverity.Warning, false, 5);
                _ = RefreshAsync();
            }
        }

        private void QualityBox_DropDownOpened(object sender, object e) => _ = RefreshAsync();
    }
}
