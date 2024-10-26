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
using System.IO.Compression;
using Newtonsoft.Json;
using Microsoft.UI.Dispatching;
using Windows.Foundation;

namespace WaveTools.Views
{
    public sealed partial class MainView : Page
    {
        private DispatcherQueue dispatcherQueue;
        private DispatcherQueueTimer dispatcherTimer_Game;
        private DispatcherQueueTimer dispatcherTimer_Launcher;

        private static readonly HttpClient httpClient = new HttpClient();
        public ObservableCollection<string> Pictures { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> PicturesClick { get; } = new ObservableCollection<string>();
        static Dictionary<string, BitmapImage> imageCache = new Dictionary<string, BitmapImage>();

        private string _url;
        List<String> list = new List<String>();
        GetNotify getNotify = new GetNotify();

        public MainView()
        {
            this.InitializeComponent();
            Logging.Write("Switch to MainView", 0);
            Loaded += MainView_Loaded;
            this.Unloaded += OnUnloaded;
            // 获取UI线程的DispatcherQueue
            InitializeDispatcherQueue();
            // 初始化并启动定时器
            InitializeTimers();
        }

        private async void MainView_Loaded(object sender, RoutedEventArgs e)
        {
            Logging.Write("MainView loaded", 0);
            LoadStartGameGrid();
            LoadBackgroundAsync();
            LoadPicturesAsync();
            LoadPostAsync();

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
            Logging.Write("Loading background", 0);
            string apiUrl = "https://prod-cn-alicdn-gamestarter.kurogame.com/pcstarter/prod/starter/10003_Y8xXrXk65DqFHEDgApn3cpK5lfczpFx5/G152/index.json";
            JObject response = await FetchData(apiUrl);

            string baseDownloadUrl = "https://pcdownload-wangsu.aki-game.com/";
            string zipUrl = baseDownloadUrl + response["animateBackground"]["url"].ToString();
            string newMd5 = response["animateBackground"]["md5"].ToString();

            string targetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"JSG-LLC\WaveTools\Background\");
            Directory.CreateDirectory(targetPath);
            string zipFilePath = Path.Combine(targetPath, "background.zip");

            string backgroundPath = Path.Combine(targetPath, "home_1.jpg");
            string iconPath = Path.Combine(targetPath, "slogan.png");
            string md5FilePath = Path.Combine(targetPath, "md5.txt");

            // 检查文件是否存在以及MD5是否匹配
            if (File.Exists(backgroundPath) && File.Exists(iconPath) && File.Exists(md5FilePath))
            {
                string cachedMd5 = await File.ReadAllTextAsync(md5FilePath);
                if (cachedMd5 == newMd5)
                {
                    // MD5匹配，直接加载
                    Logging.Write("MD5 matches, loading cached files", 0);
                    await LoadAdvertisementDataAsync(backgroundPath, iconPath);
                    return;
                }
            }

            // MD5不匹配或文件不存在，下载并解压
            Logging.Write("MD5 does not match or files do not exist, downloading and extracting new files", 0);
            await DownloadFileAsync(zipUrl, zipFilePath);
            ExtractZipFile(zipFilePath, targetPath);

            // 保存新的MD5
            await File.WriteAllTextAsync(md5FilePath, newMd5);
            LoadAdvertisementDataAsync(backgroundPath, iconPath);
        }

        private async Task LoadPicturesAsync()
        {
            Logging.Write("Loading pictures", 0);
            string apiUrl = "https://pcdownload-wangsu.aki-game.com/pcstarter/prod/starter/10003_Y8xXrXk65DqFHEDgApn3cpK5lfczpFx5/G152/guidance/zh-Hans.json";
            var response = await FetchPopulateData(apiUrl);
            var guidanceData = JsonConvert.DeserializeObject<GuidanceRoot>(response);

            // 填充图片数据
            Logging.Write("Populating pictures", 0);
            PopulatePicturesAsync(guidanceData.slideshow);
        }

        private async Task LoadPostAsync()
        {
            Logging.Write("Loading posts", 0);
            await getNotify.Get();
            NotifyLoad.Visibility = Visibility.Collapsed;
            NotifyNav.Visibility = Visibility.Visible;
            // 查找第一个已启用的MenuItem并将其选中
            foreach (var menuItem in NotifyNav.Items)
            {
                if (menuItem is SelectorBarItem item && item.IsEnabled)
                {
                    NotifyNav.SelectedItem = item;
                    Logging.Write("Selected first enabled notification item", 0);
                    break;
                }
            }
        }

        private async Task LoadAdvertisementDataAsync(string backgroundPath, string iconPath)
        {
            Logging.Write("Loading advertisement data", 0);
            BitmapImage backgroundImage = new BitmapImage(new Uri(backgroundPath));
            BackgroundImage.Source = backgroundImage;

            try
            {
                Logging.Write("Setting button icon", 0);
                BitmapImage iconImage = new BitmapImage(new Uri(iconPath));
                IconImageBrush.ImageSource = iconImage;
            }
            catch (Exception e)
            {
                Logging.Write("Error setting button icon: " + e.Message, 1);
            }

            var result = await GetUpdate.GetDependUpdate();
            var status = result.Status;
            if (status == 1)
            {
                NotificationManager.RaiseNotification("更新提示", "依赖包需要更新\n请尽快到[设置-检查依赖更新]进行更新", InfoBarSeverity.Warning);
            }
            result = await GetUpdate.GetWaveToolsUpdate();
            status = result.Status;
            if (status == 1)
            {
                NotificationManager.RaiseNotification("更新提示", "WaveTools有更新\n可到[设置-检查更新]进行更新", InfoBarSeverity.Warning);
            }

            loadRing.Visibility = Visibility.Collapsed;
        }

        private void BackgroundImage_ImageOpened(object sender, RoutedEventArgs e)
        {
            Logging.Write("Background image opened", 0);
            StartFadeAnimation(BackgroundImage, 0, 1, TimeSpan.FromSeconds(0.2));
            StartFadeAnimation(OpenUrlButton, 0, 1, TimeSpan.FromSeconds(0.2));
        }

        private void StartFadeAnimation(FrameworkElement target, double from, double to, TimeSpan duration)
        {
            Logging.Write($"Starting fade animation on {target.Name} from {from} to {to} over {duration.TotalSeconds} seconds", 0);
            DoubleAnimation opacityAnimation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = duration,
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(opacityAnimation, target);
            Storyboard.SetTargetProperty(opacityAnimation, "Opacity");

            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(opacityAnimation);
            storyboard.Begin();
        }

        private async Task<JObject> FetchData(string apiUrl)
        {
            Logging.Write("Fetching data from " + apiUrl, 0);
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
                HttpResponseMessage response = await client.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();

                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    using (var decompressedStream = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress))
                    {
                        using (var reader = new StreamReader(decompressedStream))
                        {
                            string responseBody = await reader.ReadToEndAsync();
                            return JObject.Parse(responseBody);
                        }
                    }
                }
            }
        }

        private async Task<string> FetchPopulateData(string apiUrl)
        {
            Logging.Write("Fetching populate data from " + apiUrl, 0);
            using (HttpClient client = new HttpClient())
            {
                return await client.GetStringAsync(apiUrl);
            }
        }

        public void PopulatePicturesAsync(List<Slideshow> slideshows)
        {
            Logging.Write("Populating pictures from slideshow data", 0);
            foreach (var slideshow in slideshows)
            {
                Pictures.Add(slideshow.url);
                list.Add(slideshow.jumpUrl);
            }
            FlipViewPipsPager.NumberOfPages = slideshows.Count;
            Gallery_Grid.Visibility = Visibility.Visible;
            Logging.Write("Pictures populated successfully", 0);
        }

        private void Gallery_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            Logging.Write("Gallery item pressed", 0);
            // 获取当前选中的图片
            int selectedPicture = Gallery.SelectedIndex;
            Logging.Write("Selected picture index: " + selectedPicture, 0);

            // 如果选中了图片，则打开浏览器并导航到指定的网页
            string url = list[selectedPicture]; // 替换为要打开的网页地址
            Logging.Write("Opening URL: " + url, 0);
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }

        private void Notify_NavView_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            Logging.Write("Notification navigation selection changed", 0);
            SelectorBarItem selectedItem = sender.SelectedItem;
            int currentSelectedIndex = sender.Items.IndexOf(selectedItem);
            Logging.Write("Current selected index: " + currentSelectedIndex, 0);
            switch (currentSelectedIndex)
            {
                case 0:
                    NotifyFrame.Navigate(typeof(NotifyNotificationView));
                    break;
                case 1:
                    NotifyFrame.Navigate(typeof(NotifyMessageView));
                    break;
                case 2:
                    NotifyFrame.Navigate(typeof(NotifyAnnounceView));
                    break;
            }
        }

        private async Task DownloadFileAsync(string url, string destinationPath)
        {
            Logging.Write("Downloading file from URL: " + url + " to path: " + destinationPath, 0);
            using (HttpClient client = new HttpClient())
            {
                using (HttpResponseMessage response = await client.GetAsync(url))
                {
                    response.EnsureSuccessStatusCode();
                    using (FileStream fs = new FileStream(destinationPath, FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }
            }
            Logging.Write("File downloaded successfully", 0);
        }

        private void ExtractZipFile(string zipFilePath, string extractPath)
        {
            Logging.Write("Extracting zip file: " + zipFilePath + " to path: " + extractPath, 0);
            using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destinationPath = Path.Combine(extractPath, entry.FullName);
                    entry.ExtractToFile(destinationPath, true);
                }
            }
            Logging.Write("Zip file extracted successfully", 0);
        }

        private async void SelectGame_Click(object sender, RoutedEventArgs e)
        {
            string filePath = await CommonHelpers.FileHelpers.OpenFile(".exe");
            if (filePath != null && filePath.Contains("Wuthering Waves.exe"))
            {
                // 更新为新的存储管理机制
                AppDataController.SetGamePath(filePath);
                LoadStartGameGrid();
            }
            else
            {
                NotificationManager.RaiseNotification("游戏选择无效", "选择正确的Wuthering Waves.exe\n通常位于[游戏根目录\\Wuthering Waves Game\\Wuthering Waves.exe]", InfoBarSeverity.Error);
            }
        }

        // 启动游戏
        private void StartGame_Click(object sender, RoutedEventArgs e)
        {
            StartGame(null, null);
        }
        private void StartLauncher_Click(object sender, RoutedEventArgs e)
        {
            StartLauncher(null, null);
        }

        public async void StartGame(TeachingTip sender, object args)
        {
            GameStartUtil gameStartUtil = new GameStartUtil();
            gameStartUtil.StartGame();
        }

        public void StartLauncher(TeachingTip sender, object args)
        {
            GameStartUtil gameStartUtil = new GameStartUtil();
            gameStartUtil.StartLauncher();
        }

        // 定时器回调函数，检查进程是否正在运行
        private void CheckProcess_Game(DispatcherQueueTimer timer, object e)
        {
            if (Process.GetProcessesByName("Wuthering Waves").Length > 0)
            {
                // 进程正在运行
                startGame.Visibility = Visibility.Collapsed;
                gameRunning.Visibility = Visibility.Visible;
            }
            else
            {
                // 进程未运行
                startGame.Visibility = Visibility.Visible;
                gameRunning.Visibility = Visibility.Collapsed;
            }
        }

        private void CheckProcess_Launcher(DispatcherQueueTimer timer, object e)
        {
            if (Process.GetProcessesByName("launcher").Length > 0)
            {
                // 进程正在运行
                startLauncher.Visibility = Visibility.Collapsed;
                launcherRunning.Visibility = Visibility.Visible;
            }
            else
            {
                // 进程未运行
                startLauncher.Visibility = Visibility.Visible;
                launcherRunning.Visibility = Visibility.Collapsed;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (dispatcherTimer_Game != null)
            {
                dispatcherTimer_Game.Stop();
                dispatcherTimer_Game.Tick -= CheckProcess_Game;
                dispatcherTimer_Game = null;
                Logging.Write("Game Timer Stopped", 0);
            }
            if (dispatcherTimer_Launcher != null)
            {
                dispatcherTimer_Launcher.Stop();
                dispatcherTimer_Launcher.Tick -= CheckProcess_Launcher;
                dispatcherTimer_Launcher = null;
                Logging.Write("Launcher Timer Stopped", 0);
            }
        }

        public class GuidanceRoot
        {
            public Activity guidance { get; set; }
            public string desc { get; set; }
            public List<Slideshow> slideshow { get; set; }
        }

        public class Slideshow
        {
            public string jumpUrl { get; set; }
            public string md5 { get; set; }
            public string url { get; set; }
        }
    }
}
