using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WaveTools.Depend;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using static WaveTools.App;

namespace WaveTools.Views.GachaViews
{
    public sealed partial class ScreenShotGacha : Page
    {
        private readonly GachaReportOptions _options;
        private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource _lifetime = new();
        private readonly List<string> _savedFiles = new();
        private GachaReportBuilder _report;
        private byte[] _currentPng;
        private int _currentPage;
        private bool _started;
        private bool _ready;
        private bool _changingPage;
        private bool _saving;
        private bool _closed;

        public Window CurrentWindow { get; set; }
        public Task<bool> Completion => _completion.Task;
        public IReadOnlyList<string> SavedFiles => _savedFiles;
        public event EventHandler ReportReady;

        public ScreenShotGacha(GachaReportOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            InitializeComponent();
            Loaded += Report_Loaded;
        }

        private async void Report_Loaded(object sender, RoutedEventArgs e)
        {
            if (_started || _closed) return;
            _started = true;
            try
            {
                string path = Path.Combine(AppDataController.GetDataPath("GachaRecords"), _options.Uid + ".json");
                var data = JsonConvert.DeserializeObject<GachaModel.GachaData>(await File.ReadAllTextAsync(path, _lifetime.Token));
                var pools = data?.List?.Where(pool => pool != null && pool.CardPoolId == _options.PoolId).ToList();
                var records = pools?.Where(pool => pool.Records != null).SelectMany(pool => pool.Records)
                    .Where(record => record != null).ToList();
                if (records == null || records.Count == 0) throw new InvalidOperationException("当前卡池没有抽卡记录");
                var images = await LoadPicturesAsync(records);
                var logo = await DecodePictureAsync(Path.Combine(AppContext.BaseDirectory, "Imgs", "Logo_Background.png"));
                GachaReportImage Icon(GachaModel.GachaRecord record) => images.TryGetValue(ImageKey(record), out var image) ? image : null;
                _report = new GachaReportBuilder(records,
                    _options.Rule?.CardPoolType ?? pools.FirstOrDefault()?.CardPoolType ?? "抽卡记录",
                    _options, CommonHelpers.AppVersionHelper.GetAppVersion(), logo, Icon);
                await ShowPageAsync(0);
                _ready = true;
                ReportLoading.IsActive = false;
                ReportLoading.Visibility = Visibility.Collapsed;
                ReportStatus.Text = "高清 PNG · 纵向长图";
                UpdateControls();
                ReportReady?.Invoke(this, EventArgs.Empty);
                if (_options.AutoSave) await SaveAsync();
            }
            catch (OperationCanceledException) when (_closed) { }
            catch (Exception ex)
            {
                if (!_closed)
                {
                    Logging.Write($"Create gacha report failed: {ex}", 2);
                    NotificationManager.RaiseNotification("生成截图失败", ex.Message, InfoBarSeverity.Error, false, 6);
                    CurrentWindow?.Close();
                    _completion.TrySetResult(false);
                }
            }
        }

        private static string ImageKey(GachaModel.GachaRecord record) =>
            $"{record.ResourceType}|{record.ResourceId}|{record.Name}";

        private async Task<Dictionary<string, GachaReportImage>> LoadPicturesAsync(List<GachaModel.GachaRecord> records)
        {
            var result = new Dictionary<string, GachaReportImage>(StringComparer.Ordinal);
            string root = Path.Combine(AppDataController.GetDataPath("Cache"), "WuwaAssets");
            string directory = Path.Combine(root, "icons");
            if (!Directory.Exists(directory)) return result;
            JObject index = null;
            try
            {
                string path = Path.Combine(root, "wuwa-assets.json");
                if (File.Exists(path)) index = JObject.Parse(await File.ReadAllTextAsync(path, _lifetime.Token));
            }
            catch (JsonException ex) { Logging.Write($"Read report asset index failed: {ex.Message}", 1); }
            catch (IOException ex) { Logging.Write($"Read report asset index failed: {ex.Message}", 1); }
            var files = Directory.EnumerateFiles(directory)
                .Where(file => new[] { ".png", ".jpg", ".jpeg", ".webp", ".avif" }.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(File.GetLastWriteTimeUtc).ToList(), StringComparer.Ordinal);
            foreach (var record in records.Where(record => record.QualityLevel == 5).DistinctBy(ImageKey))
            {
                _lifetime.Token.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(record.ResourceId) || !files.TryGetValue(record.ResourceId, out var candidates)) continue;
                JToken asset = record.ResourceType?.Contains("武器") == true ? index?["weapon"]?[record.ResourceId]
                    : index?["role"]?[record.ResourceId] ?? index?["weapon"]?[record.ResourceId];
                string expectedName = asset?.Value<string>("name");
                if (!string.IsNullOrWhiteSpace(expectedName) &&
                    !string.Equals(expectedName.Trim(), record.Name?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    Logging.Write($"Report icon name mismatch: resourceId={record.ResourceId}", 1);
                    continue;
                }
                string file = candidates[0];
                if (Uri.TryCreate(asset?.Value<string>("icon"), UriKind.Absolute, out var uri))
                {
                    string extension = Path.GetExtension(uri.AbsolutePath);
                    file = candidates.FirstOrDefault(candidate => string.Equals(Path.GetExtension(candidate), extension, StringComparison.OrdinalIgnoreCase)) ?? file;
                }
                var picture = await DecodePictureAsync(file);
                if (picture != null) result[ImageKey(record)] = picture;
            }
            return result;
        }

        private async Task<GachaReportImage> DecodePictureAsync(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var stream = file.AsRandomAccessStream();
                var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(_lifetime.Token);
                double ratio = Math.Min(1, 256.0 / Math.Max(decoder.PixelWidth, decoder.PixelHeight));
                uint width = (uint)Math.Max(1, Math.Round(decoder.PixelWidth * ratio));
                uint height = (uint)Math.Max(1, Math.Round(decoder.PixelHeight * ratio));
                var provider = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                    new BitmapTransform { ScaledWidth = width, ScaledHeight = height },
                    ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage).AsTask(_lifetime.Token);
                return new GachaReportImage { Width = (int)width, Height = (int)height, Pixels = provider.DetachPixelData() };
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Logging.Write($"Decode report image failed: {Path.GetFileName(path)}: {ex.Message}", 1);
                return null;
            }
        }

        private async Task<byte[]> RenderPageAsync(int index) =>
            await Task.Run(() => _report.RenderPage(index, _lifetime.Token), _lifetime.Token);

        private async Task ShowPageAsync(int index)
        {
            byte[] png = await RenderPageAsync(index);
            _lifetime.Token.ThrowIfCancellationRequested();
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(png);
                await writer.StoreAsync().AsTask(_lifetime.Token);
                writer.DetachStream();
            }
            stream.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream).AsTask(_lifetime.Token);
            _lifetime.Token.ThrowIfCancellationRequested();
            ReportImage.Source = bitmap;
            _currentPng = png;
            _currentPage = index;
            UpdateControls();
        }

        private void UpdateControls()
        {
            if (_closed) return;
            SaveReportButton.IsEnabled = _ready && !_saving && !_changingPage;
            PreviousPageButton.IsEnabled = _ready && !_saving && !_changingPage && _currentPage > 0;
            NextPageButton.IsEnabled = _ready && !_saving && !_changingPage && _currentPage + 1 < (_report?.PageCount ?? 0);
            PageIndicator.Text = _report == null ? "0 / 0" : $"{_currentPage + 1} / {_report.PageCount}";
        }

        private async void PreviousPage_Click(object sender, RoutedEventArgs e) => await NavigateAsync(-1);
        private async void NextPage_Click(object sender, RoutedEventArgs e) => await NavigateAsync(1);
        private async Task NavigateAsync(int delta)
        {
            if (!_ready || _saving || _changingPage || _closed) return;
            int index = _currentPage + delta;
            if (index < 0 || index >= _report.PageCount) return;
            _changingPage = true;
            UpdateControls();
            try { await ShowPageAsync(index); }
            catch (OperationCanceledException) when (_closed) { }
            catch (Exception ex)
            {
                if (!_closed)
                {
                    Logging.Write($"Change report page failed: {ex}", 2);
                    NotificationManager.RaiseNotification("预览加载失败", ex.Message, InfoBarSeverity.Error, false, 5);
                }
            }
            finally { _changingPage = false; UpdateControls(); }
        }

        private async void SaveReport_Click(object sender, RoutedEventArgs e) => await SaveAsync();
        private void CloseReport_Click(object sender, RoutedEventArgs e) => CurrentWindow?.Close();

        private async Task SaveAsync()
        {
            if (!_ready || _saving || _changingPage || _closed) return;
            _saving = true;
            UpdateControls();
            ReportLoading.Visibility = Visibility.Visible;
            ReportLoading.IsActive = true;
            var createdFiles = new List<string>();
            try
            {
                string directory = AppDataController.GetDataPath("GachaScreenshots");
                Directory.CreateDirectory(directory);
                string identity = _options.HideUid ? "HiddenUID" : string.Concat(_options.Uid.Where(char.IsDigit));
                string name = $"Gacha_{identity}_{_options.PoolId}_{DateTime.Now:yyyyMMdd_HHmmssfff}";
                for (int i = 0; i < _report.PageCount; i++)
                {
                    _lifetime.Token.ThrowIfCancellationRequested();
                    ReportStatus.Text = $"正在保存 {i + 1} / {_report.PageCount}";
                    byte[] png = i == _currentPage && _currentPng != null ? _currentPng : await RenderPageAsync(i);
                    string suffix = _report.PageCount > 1 ? $"_{i + 1:D2}" : "";
                    string file = Path.Combine(directory, name + suffix + ".png");
                    await using (var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        createdFiles.Add(file);
                        await stream.WriteAsync(png, _lifetime.Token);
                    }
                }
                _savedFiles.AddRange(createdFiles);
                CurrentWindow?.Close();
                _completion.TrySetResult(true);
            }
            catch (Exception ex)
            {
                foreach (string file in createdFiles)
                {
                    try { File.Delete(file); }
                    catch (Exception cleanupError) { Logging.Write($"Clean incomplete report failed: {cleanupError.Message}", 1); }
                }
                if (!_closed)
                {
                    Logging.Write($"Save gacha report failed: {ex}", 2);
                    NotificationManager.RaiseNotification("保存截图失败", ex.Message, InfoBarSeverity.Error, false, 6);
                    ReportStatus.Text = "保存失败，可重试";
                }
            }
            finally
            {
                _saving = false;
                if (!_closed)
                {
                    ReportLoading.IsActive = false;
                    ReportLoading.Visibility = Visibility.Collapsed;
                    UpdateControls();
                }
            }
        }

        public void NotifyWindowClosed()
        {
            if (_closed) return;
            _closed = true;
            _lifetime.Cancel();
            _completion.TrySetResult(_savedFiles.Count > 0);
        }
    }
}
