using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using Microsoft.UI.Xaml.Media.Animation;
using WaveTools.Depend;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Input;
using WaveTools.Views.NotifyViews;
using System.IO;
using static WaveTools.App;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Microsoft.UI.Dispatching;
using Windows.Foundation;
using Windows.Media.Core;
using System.Net;

namespace WaveTools.Views
{
    public class ContentItem { public string content { get; set; } public string jumpUrl { get; set; } public string time { get; set; } }
    public class GuidanceSection { public string title { get; set; } public int sort { get; set; } public int functionSwitch { get; set; } public List<ContentItem> contents { get; set; } }
    public class Guidance { public string desc { get; set; } public GuidanceSection activity { get; set; } public GuidanceSection notice { get; set; } public GuidanceSection news { get; set; } }
    public class SlideshowItem { public string url { get; set; } public string jumpUrl { get; set; } public string md5 { get; set; } public string carouselNotes { get; set; } }
    public class InformationRoot { public Guidance guidance { get; set; } public List<SlideshowItem> slideshow { get; set; } }
    public static class NotificationDataHolder { public static List<ContentItem> ActivityContents { get; set; } public static List<ContentItem> NoticeContents { get; set; } public static List<ContentItem> NewsContents { get; set; } }

    public sealed partial class MainView : Page
    {
        // Class to hold data for navigation to the gallery view
        public class GalleryNavigationData
        {
            public ObservableCollection<string> Pictures { get; set; }
            public List<string> JumpUrls { get; set; }
        }

        private DispatcherQueue dispatcherQueue;
        private DispatcherQueueTimer dispatcherTimer_Game;
        private DispatcherQueueTimer dispatcherTimer_Launcher;
        private static readonly HttpClient httpClient = new HttpClient(new HttpClientHandler()
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        });
        private readonly string cachePath;

        public ObservableCollection<string> Pictures { get; } = new ObservableCollection<string>();
        List<String> list = new List<String>();
        GetNotify getNotify = new GetNotify();

        public MainView()
        {
            this.InitializeComponent();
            Logging.Write("Switch to MainView", 0);

            cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"JSG-LLC\WaveTools\Cache\");
            Directory.CreateDirectory(cachePath);
            Logging.Write($"Cache path set to: {cachePath}", 0);

            Loaded += MainView_Loaded;
            this.Unloaded += OnUnloaded;

            InitializeDispatcherQueue();
            InitializeTimers();
        }

        private async void MainView_Loaded(object sender, RoutedEventArgs e)
        {
            Logging.Write("MainView loaded", 0);
            if (BackgroundMediaPlayer.MediaPlayer != null)
            {
                BackgroundMediaPlayer.MediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
            }

            LoadStartGameGrid();
            try
            {
                await getNotify.Get();
                Notify_Grid.Visibility = Visibility.Visible;
                Logging.Write("Notifications loaded successfully", 0);
            }
            catch (Exception ex)
            {
                Logging.Write("Failed to load notifications: " + ex.Message, 1);
                loadRing.Visibility = Visibility.Collapsed;
                loadErr.Visibility = Visibility.Visible;
            }
            await LoadBackgroundAsync();
            await LoadInformationAsync();
        }

        private void InitializeDispatcherQueue()
        {
            dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        }

        private void InitializeTimers()
        {
            dispatcherTimer_Game = CreateTimer(TimeSpan.FromSeconds(0.2), CheckProcess_Game);
            dispatcherTimer_Game.Start();
            dispatcherTimer_Launcher = CreateTimer(TimeSpan.FromSeconds(0.2), CheckProcess_Launcher);
            dispatcherTimer_Launcher.Start();
        }

        private DispatcherQueueTimer CreateTimer(TimeSpan interval, TypedEventHandler<DispatcherQueueTimer, object> tickHandler)
        {
            var timer = dispatcherQueue.CreateTimer();
            timer.Interval = interval;
            timer.Tick += tickHandler;
            timer.Start();
            return timer;
        }

        private void LoadStartGameGrid()
        {
            if (AppDataController.GetGamePath() != null)
            {
                string GamePath = AppDataController.GetGamePath();
                Logging.Write("GamePath: " + GamePath, 0);
                if (!string.IsNullOrEmpty(GamePath) && GamePath.Contains("Null"))
                {
                    UpdateUIElementsVisibility(0);
                }
                else
                {
                    UpdateUIElementsVisibility(1);
                }
            }
            else
            {
                UpdateUIElementsVisibility(0);
            }
        }

        private void UpdateUIElementsVisibility(int status)
        {
            if (status == 0)
            {
                startGame.IsEnabled = false;
                startLauncher.IsEnabled = false;
                StartGame_Grid.Visibility = Visibility.Collapsed;
                selectGame.IsEnabled = true;
                SelectGame_Grid.Visibility = Visibility.Visible;
            }
            else
            {
                startGame.IsEnabled = true;
                startLauncher.IsEnabled = true;
                StartGame_Grid.Visibility = Visibility.Visible;
                selectGame.IsEnabled = false;
                SelectGame_Grid.Visibility = Visibility.Collapsed;
            }
        }

        private async Task LoadBackgroundAsync()
        {
            Logging.Write("Loading background with caching", 0);
            try
            {
                string firstApiUrl = "https://prod-cn-alicdn-gamestarter.kurogame.com/launcher/launcher/10003_Y8xXrXk65DqFHEDgApn3cpK5lfczpFx5/G152/index.json";
                JObject firstResponse = await FetchJsonAsync(firstApiUrl);
                string backgroundCode = firstResponse["functionCode"]["background"]?.ToString();
                if (string.IsNullOrEmpty(backgroundCode)) throw new Exception("Background code not found.");

                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string secondApiUrl = $"https://prod-cn-alicdn-gamestarter.kurogame.com/launcher/10003_Y8xXrXk65DqFHEDgApn3cpK5lfczpFx5/G152/background/{backgroundCode}/zh-Hans.json?_t={timestamp}";
                JObject secondResponse = await FetchJsonAsync(secondApiUrl);

                string backgroundVideoUrl = secondResponse["backgroundFile"]?.ToString();
                string sloganImageUrl = secondResponse["slogan"]?.ToString();
                if (string.IsNullOrEmpty(backgroundVideoUrl) || string.IsNullOrEmpty(sloganImageUrl)) throw new Exception("Background video or slogan URL not found.");

                string videoFileName = Path.GetFileName(new Uri(backgroundVideoUrl).LocalPath);
                string sloganFileName = Path.GetFileName(new Uri(sloganImageUrl).LocalPath);

                string localVideoPath = Path.Combine(cachePath, videoFileName);
                string localSloganPath = Path.Combine(cachePath, sloganFileName);

                if (!File.Exists(localVideoPath)) await DownloadFileAsync(backgroundVideoUrl, localVideoPath);
                else Logging.Write($"Video found in cache: {localVideoPath}", 0);

                if (!File.Exists(localSloganPath)) await DownloadFileAsync(sloganImageUrl, localSloganPath);
                else Logging.Write($"Slogan found in cache: {localSloganPath}", 0);

                await LoadAdvertisementDataAsync(localVideoPath, localSloganPath);
            }
            catch (Exception ex)
            {
                Logging.Write("Failed to load background: " + ex.Message, 2);
                loadRing.Visibility = Visibility.Collapsed;
                loadErr.Visibility = Visibility.Visible;
            }
        }

        private async Task LoadAdvertisementDataAsync(string backgroundVideoPath, string sloganImagePath)
        {
            Logging.Write("Loading advertisement data from local cache", 0);
            try
            {
                BackgroundMediaPlayer.Source = MediaSource.CreateFromUri(new Uri(backgroundVideoPath));
                BackgroundMediaPlayer.MediaPlayer.IsLoopingEnabled = true;
                Logging.Write($"Background video source set from: {backgroundVideoPath}", 0);
            }
            catch (Exception e)
            {
                Logging.Write("Error setting background video source: " + e.Message, 2);
            }
            try
            {
                BitmapImage iconImage = new BitmapImage(new Uri(sloganImagePath));
                IconImageBrush.ImageSource = iconImage;
                Logging.Write($"Slogan image source set from: {sloganImagePath}", 0);
            }
            catch (Exception e)
            {
                Logging.Write("Error setting button icon: " + e.Message, 1);
            }

            var result = await GetUpdate.GetDependUpdate();
            if (result.Status == 1) NotificationManager.RaiseNotification("更新提示", "依赖包需要更新\n请尽快到[设置-检查依赖更新]进行更新", InfoBarSeverity.Warning);

            result = await GetUpdate.GetWaveToolsUpdate();
            if (result.Status == 1) NotificationManager.RaiseNotification("更新提示", "WaveTools有更新\n可到[设置-检查更新]进行更新", InfoBarSeverity.Warning);

            loadRing.Visibility = Visibility.Collapsed;
        }

        private async Task LoadInformationAsync()
        {
            Logging.Write("Loading all information from new API with caching", 0);
            try
            {
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string apiUrl = $"https://prod-cn-alicdn-gamestarter.kurogame.com/launcher/10003_Y8xXrXk65DqFHEDgApn3cpK5lfczpFx5/G152/information/zh-Hans.json?_t={timestamp}";

                string jsonResponse = await FetchStringAsync(apiUrl);
                var informationData = JsonConvert.DeserializeObject<InformationRoot>(jsonResponse);

                if (informationData?.slideshow != null)
                {
                    await PopulatePicturesAsync(informationData.slideshow);
                    Logging.Write("Pictures populated successfully.", 0);
                }

                if (informationData?.guidance != null)
                {
                    PopulateNotifications(informationData.guidance);
                    Logging.Write("Notifications populated successfully.", 0);
                }
            }
            catch (Exception ex)
            {
                Logging.Write("Failed to load information from new API: " + ex.Message, 2);
                Notify_Grid.Visibility = Visibility.Collapsed;
                loadRing.Visibility = Visibility.Collapsed;
                loadErr.Visibility = Visibility.Visible;
            }
        }

        public async Task PopulatePicturesAsync(List<SlideshowItem> slideshows)
        {
            Logging.Write("Populating pictures from slideshow data with caching", 0);
            Pictures.Clear();
            list.Clear();
            foreach (var slideshow in slideshows)
            {
                try
                {
                    string imageFileName = Path.GetFileName(new Uri(slideshow.url).LocalPath);
                    string localImagePath = Path.Combine(cachePath, imageFileName);

                    if (!File.Exists(localImagePath)) await DownloadFileAsync(slideshow.url, localImagePath);
                    else Logging.Write($"Slideshow image found in cache: {localImagePath}", 0);

                    Pictures.Add(localImagePath);
                    list.Add(slideshow.jumpUrl);
                }
                catch (Exception ex)
                {
                    Logging.Write($"Failed to process slideshow item {slideshow.url}: {ex.Message}", 1);
                }
            }
        }

        public void PopulateNotifications(Guidance guidance)
        {
            NotificationDataHolder.ActivityContents = guidance.activity?.contents;
            NotificationDataHolder.NoticeContents = guidance.notice?.contents;
            NotificationDataHolder.NewsContents = guidance.news?.contents;
            NotifyLoad.Visibility = Visibility.Collapsed;
            Notify_Grid.Visibility = Visibility.Visible;
            NotifyNav.Visibility = Visibility.Visible;

            // This loop will now automatically select the "Gallery" tab first if it's available.
            foreach (var menuItem in NotifyNav.Items)
            {
                if (menuItem is SelectorBarItem item && item.IsEnabled)
                {
                    NotifyNav.SelectedItem = item;
                    break;
                }
            }
        }

        private async Task<JObject> FetchJsonAsync(string apiUrl)
        {
            Logging.Write("Fetching JSON from " + apiUrl, 0);
            string responseBody = await httpClient.GetStringAsync(apiUrl);
            return JObject.Parse(responseBody);
        }

        private async Task<string> FetchStringAsync(string apiUrl)
        {
            Logging.Write("Fetching string from " + apiUrl, 0);
            return await httpClient.GetStringAsync(apiUrl);
        }

        private async Task DownloadFileAsync(string url, string destinationPath)
        {
            Logging.Write($"Downloading file from {url} to {destinationPath}", 0);
            try
            {
                string tempPath = destinationPath + ".tmp";
                using (HttpResponseMessage response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    using (Stream contentStream = await response.Content.ReadAsStreamAsync(),
                                fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        await contentStream.CopyToAsync(fileStream);
                    }
                }
                File.Move(tempPath, destinationPath, true);
                Logging.Write($"File downloaded successfully: {destinationPath}", 0);
            }
            catch (Exception ex)
            {
                Logging.Write($"Failed to download file: {ex.Message}", 2);
                throw;
            }
        }

        private void MediaPlayer_MediaOpened(Windows.Media.Playback.MediaPlayer sender, object args)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                Logging.Write("Background media opened, starting fade animation.", 0);
                StartFadeAnimation(BackgroundMediaPlayer, 0, 1, TimeSpan.FromSeconds(0.5));
                StartFadeAnimation(OpenUrlButton, 0, 1, TimeSpan.FromSeconds(0.5));
            });
        }

        private void StartFadeAnimation(FrameworkElement target, double from, double to, TimeSpan duration)
        {
            DoubleAnimation opacityAnimation = new DoubleAnimation { From = from, To = to, Duration = duration, EnableDependentAnimation = true };
            Storyboard.SetTarget(opacityAnimation, target);
            Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(opacityAnimation);
            storyboard.Begin();
        }

        private void Notify_NavView_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            if (sender.SelectedItem is SelectorBarItem selectedItem && selectedItem.Tag is string tag)
            {
                var transitionInfo = new SuppressNavigationTransitionInfo();
                switch (tag)
                {
                    case "Notify_Gallery":
                        var navData = new GalleryNavigationData { Pictures = this.Pictures, JumpUrls = this.list };
                        NotifyFrame.Navigate(typeof(NotifyGalleryView), navData, transitionInfo);
                        break;
                    case "Notify_Notification":
                        NotifyFrame.Navigate(typeof(NotifyNotificationView), null, transitionInfo);
                        break;
                    case "Notify_Message":
                        NotifyFrame.Navigate(typeof(NotifyMessageView), null, transitionInfo);
                        break;
                    case "Notify_Announce":
                        NotifyFrame.Navigate(typeof(NotifyAnnounceView), null, transitionInfo);
                        break;
                }
            }
        }

        private async void SelectGame_Click(object sender, RoutedEventArgs e)
        {
            string filePath = await CommonHelpers.FileHelpers.OpenFile(".exe");
            if (filePath != null && filePath.Contains("Wuthering Waves.exe"))
            {
                AppDataController.SetGamePath(filePath);
                LoadStartGameGrid();
            }
            else
            {
                NotificationManager.RaiseNotification("游戏选择无效", "选择正确的Wuthering Waves.exe\n通常位于[游戏根目录\\Wuthering Waves Game\\Wuthering Waves.exe]", InfoBarSeverity.Error);
            }
        }

        private void StartGame_Click(object sender, RoutedEventArgs e) { new GameStartUtil().StartGame(); }
        private void StartLauncher_Click(object sender, RoutedEventArgs e) { new GameStartUtil().StartLauncher(); }

        private void CheckProcess_Game(DispatcherQueueTimer timer, object e)
        {
            bool isRunning = Process.GetProcessesByName("Wuthering Waves").Length > 0;
            startGame.Visibility = isRunning ? Visibility.Collapsed : Visibility.Visible;
            gameRunning.Visibility = isRunning ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CheckProcess_Launcher(DispatcherQueueTimer timer, object e)
        {
            bool isRunning = Process.GetProcessesByName("launcher").Length > 0;
            startLauncher.Visibility = isRunning ? Visibility.Collapsed : Visibility.Visible;
            launcherRunning.Visibility = isRunning ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (dispatcherTimer_Game != null)
            {
                dispatcherTimer_Game.Stop();
                dispatcherTimer_Game.Tick -= CheckProcess_Game;
                dispatcherTimer_Game = null;
            }
            if (dispatcherTimer_Launcher != null)
            {
                dispatcherTimer_Launcher.Stop();
                dispatcherTimer_Launcher.Tick -= CheckProcess_Launcher;
                dispatcherTimer_Launcher = null;
            }
            if (BackgroundMediaPlayer.MediaPlayer != null)
            {
                BackgroundMediaPlayer.MediaPlayer.MediaOpened -= MediaPlayer_MediaOpened;
            }
        }
    }
}