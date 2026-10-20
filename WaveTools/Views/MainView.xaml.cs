// Copyright (c) 2021-2024, JamXi JSG-LLC.
// All rights reserved.

// This file is part of WaveTools.

// WaveTools is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// WaveTools is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with WaveTools.  If not, see <http://www.gnu.org/licenses/>.

// For more information, please refer to <https://www.gnu.org/licenses/gpl-3.0.html>

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using WaveTools.Depend;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.ObjectModel;
using WaveTools.Views.NotifyViews;
using System.IO;
using static WaveTools.App;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Microsoft.UI.Dispatching;
using Windows.Foundation;
using System.Net;
using Microsoft.UI.Xaml.Media.Animation;

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
        private readonly List<string> list = new List<string>();
        private static bool homeLoadedInCurrentProcess;
        private static bool updateCheckedInCurrentProcess;
        private static string cachedBackgroundVideoPath;
        private static string cachedSloganImagePath;
        private static List<string> cachedPicturePaths = new List<string>();
        private static List<string> cachedJumpUrls = new List<string>();
        private static List<ContentItem> cachedActivityContents = new List<ContentItem>();
        private static List<ContentItem> cachedNoticeContents = new List<ContentItem>();
        private static List<ContentItem> cachedNewsContents = new List<ContentItem>();
        public static WeakReference<MainView> CurrentInstance { get; private set; }
        private static bool isPreparingDataRootMove;
        private bool isPageUnloaded;

        private bool CanTouchUi()
        {
            return !isPageUnloaded && !isPreparingDataRootMove;
        }

        private async Task WaitForCachePreparationAsync()
        {
            while (isPreparingDataRootMove && !isPageUnloaded)
            {
                await Task.Delay(50);
            }
        }

        public MainView()
        {
            InitializeComponent();
            Logging.Write("Switch to MainView", 0);

            cachePath = AppDataController.GetDataPath("Cache") + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(cachePath);
            Logging.Write($"Cache path set to: {cachePath}", 0);

            Loaded += MainView_Loaded;
            Unloaded += OnUnloaded;

            InitializeDispatcherQueue();
            InitializeTimers();
        }

        private async void MainView_Loaded(object sender, RoutedEventArgs e)
        {
            isPageUnloaded = false;
            CurrentInstance = new WeakReference<MainView>(this);

            Logging.Write("MainView loaded", 0);

            if (isPreparingDataRootMove)
            {
                await WaitForCachePreparationAsync();
                if (isPageUnloaded)
                {
                    return;
                }
            }

            LoadStartGameGrid();

            if (TryApplyHomeCache())
            {
                Logging.Write("MainView home cache applied, skip network loading", 0);
                App.MainWindow?.SetHomeBackgroundVideoMode(true);
                return;
            }

            await LoadHomeForCurrentProcessAsync();
        }

        private async Task LoadHomeForCurrentProcessAsync()
        {
            if (!CanTouchUi())
            {
                return;
            }

            loadRing.Visibility = Visibility.Visible;
            loadErr.Visibility = Visibility.Collapsed;

            try
            {
                await LoadBackgroundAsync();
                if (!CanTouchUi())
                {
                    return;
                }

                await LoadInformationAsync();
                if (!CanTouchUi())
                {
                    return;
                }

                homeLoadedInCurrentProcess = true;

                loadRing.Visibility = Visibility.Collapsed;
                loadErr.Visibility = Visibility.Collapsed;
                App.MainWindow?.SetHomeBackgroundVideoMode(true);

                Logging.Write("MainView home loaded and cached for current process", 0);
            }
            catch (Exception ex)
            {
                if (!CanTouchUi())
                {
                    return;
                }

                Logging.Write("Failed to load MainView home: " + ex.Message, 2);

                loadRing.Visibility = Visibility.Collapsed;

                if (!TryApplyHomeCache())
                {
                    loadErr.Visibility = Visibility.Visible;
                }
            }
        }

        private bool TryApplyHomeCache()
        {
            if (isPreparingDataRootMove)
            {
                return true;
            }

            if (!homeLoadedInCurrentProcess)
            {
                return false;
            }

            if (string.IsNullOrEmpty(cachedBackgroundVideoPath) ||
                string.IsNullOrEmpty(cachedSloganImagePath) ||
                !File.Exists(cachedBackgroundVideoPath) ||
                !File.Exists(cachedSloganImagePath))
            {
                homeLoadedInCurrentProcess = false;
                return false;
            }

            try
            {
                ApplyBackgroundFromCache(cachedBackgroundVideoPath, cachedSloganImagePath);
                ApplyInformationFromCache();

                loadRing.Visibility = Visibility.Collapsed;
                loadErr.Visibility = Visibility.Collapsed;

                return true;
            }
            catch (Exception ex)
            {
                Logging.Write("Failed to apply MainView cache: " + ex.Message, 2);
                return false;
            }
        }

        private void ApplyBackgroundFromCache(string backgroundVideoPath, string sloganImagePath)
        {
            if (!CanTouchUi())
            {
                return;
            }

            try
            {
                App.MainWindow?.ShowHomeBackgroundVideo(backgroundVideoPath);
                Logging.Write($"Home background video requested from cache: {backgroundVideoPath}", 0);
            }
            catch (Exception ex)
            {
                Logging.Write("Error showing cached background video: " + ex.Message, 2);
            }

            try
            {
                IconImageBrush.ImageSource = new BitmapImage(new Uri(sloganImagePath));
                OpenUrlButton.Opacity = 1;
                Logging.Write($"Slogan image source set from cache: {sloganImagePath}", 0);
            }
            catch (Exception ex)
            {
                Logging.Write("Error setting cached slogan image: " + ex.Message, 1);
            }
        }

        private void ApplyInformationFromCache()
        {
            if (!CanTouchUi())
            {
                return;
            }

            Pictures.Clear();
            list.Clear();

            foreach (string picturePath in cachedPicturePaths)
            {
                if (File.Exists(picturePath))
                {
                    Pictures.Add(picturePath);
                }
            }

            foreach (string jumpUrl in cachedJumpUrls)
            {
                list.Add(jumpUrl);
            }

            NotificationDataHolder.ActivityContents = cachedActivityContents;
            NotificationDataHolder.NoticeContents = cachedNoticeContents;
            NotificationDataHolder.NewsContents = cachedNewsContents;

            NotifyLoad.Visibility = Visibility.Collapsed;
            Notify_Grid.Visibility = Visibility.Visible;
            NotifyNav.Visibility = Visibility.Visible;

            SelectFirstAvailableNotifyItem();
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
            Logging.Write("Loading background with file cache", 0);

            string firstApiUrl = "https://prod-cn-alicdn-gamestarter.kurogame.com/launcher/launcher/10003_Y8xXrXk65DqFHEDgApn3cpK5lfczpFx5/G152/index.json";
            JObject firstResponse = await FetchJsonAsync(firstApiUrl);

            string backgroundCode = firstResponse["functionCode"]?["background"]?.ToString();
            if (string.IsNullOrEmpty(backgroundCode))
            {
                throw new Exception("Background code not found.");
            }

            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string secondApiUrl = $"https://prod-cn-alicdn-gamestarter.kurogame.com/launcher/10003_Y8xXrXk65DqFHEDgApn3cpK5lfczpFx5/G152/background/{backgroundCode}/zh-Hans.json?_t={timestamp}";
            JObject secondResponse = await FetchJsonAsync(secondApiUrl);

            string backgroundVideoUrl = secondResponse["backgroundFile"]?.ToString();
            string sloganImageUrl = secondResponse["slogan"]?.ToString();

            if (string.IsNullOrEmpty(backgroundVideoUrl) || string.IsNullOrEmpty(sloganImageUrl))
            {
                throw new Exception("Background video or slogan URL not found.");
            }

            string videoFileName = Path.GetFileName(new Uri(backgroundVideoUrl).LocalPath);
            string sloganFileName = Path.GetFileName(new Uri(sloganImageUrl).LocalPath);

            string localVideoPath = Path.Combine(cachePath, videoFileName);
            string localSloganPath = Path.Combine(cachePath, sloganFileName);

            if (!File.Exists(localVideoPath))
            {
                await DownloadFileAsync(backgroundVideoUrl, localVideoPath);
            }
            else
            {
                Logging.Write($"Video found in file cache: {localVideoPath}", 0);
            }

            if (!File.Exists(localSloganPath))
            {
                await DownloadFileAsync(sloganImageUrl, localSloganPath);
            }
            else
            {
                Logging.Write($"Slogan found in file cache: {localSloganPath}", 0);
            }

            cachedBackgroundVideoPath = localVideoPath;
            cachedSloganImagePath = localSloganPath;

            await LoadAdvertisementDataAsync(localVideoPath, localSloganPath);
        }

        private async Task LoadAdvertisementDataAsync(string backgroundVideoPath, string sloganImagePath)
        {
            ApplyBackgroundFromCache(backgroundVideoPath, sloganImagePath);

            if (!updateCheckedInCurrentProcess)
            {
                updateCheckedInCurrentProcess = true;

                var result = await GetUpdate.GetDependUpdate();
                if (result.Status == 1)
                {
                    NotificationManager.RaiseNotification("更新提示", "依赖包需要更新\n请尽快到[设置-检查依赖更新]进行更新", InfoBarSeverity.Warning);
                }

                result = await GetUpdate.GetWaveToolsUpdate();
                if (result.Status == 1)
                {
                    NotificationManager.RaiseNotification("更新提示", "WaveTools有更新\n可到[设置-检查更新]进行更新", InfoBarSeverity.Warning);
                }
            }
        }

        private async Task LoadInformationAsync()
        {
            Logging.Write("Loading information with file cache", 0);

            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string apiUrl = $"https://prod-cn-alicdn-gamestarter.kurogame.com/launcher/10003_Y8xXrXk65DqFHEDgApn3cpK5lfczpFx5/G152/information/zh-Hans.json?_t={timestamp}";

            string jsonResponse = await FetchStringAsync(apiUrl);
            var informationData = JsonConvert.DeserializeObject<InformationRoot>(jsonResponse);

            if (informationData?.slideshow != null)
            {
                await PopulatePicturesAsync(informationData.slideshow);
                cachedPicturePaths = new List<string>(Pictures);
                cachedJumpUrls = new List<string>(list);
                Logging.Write("Pictures populated and cached successfully.", 0);
            }

            if (informationData?.guidance != null)
            {
                await RefreshNotificationFilesAsync();
                PopulateNotifications(informationData.guidance);
                Logging.Write("Notifications populated and cached successfully.", 0);
            }
        }

        private static async Task RefreshNotificationFilesAsync()
        {
            try
            {
                GetNotify getNotify = new GetNotify();
                await getNotify.Get();
                Logging.Write("Notification files refreshed successfully.", 0);
            }
            catch (Exception ex)
            {
                Logging.Write("Failed to refresh notification files: " + ex.Message, 2);
            }
        }

        public async Task PopulatePicturesAsync(List<SlideshowItem> slideshows)
        {
            if (!CanTouchUi())
            {
                return;
            }

            Logging.Write("Populating pictures from slideshow data with file cache", 0);

            Pictures.Clear();
            list.Clear();

            foreach (var slideshow in slideshows)
            {
                try
                {
                    if (string.IsNullOrEmpty(slideshow.url))
                    {
                        continue;
                    }

                    string imageFileName = Path.GetFileName(new Uri(slideshow.url).LocalPath);
                    string localImagePath = Path.Combine(cachePath, imageFileName);

                    if (!File.Exists(localImagePath))
                    {
                        await DownloadFileAsync(slideshow.url, localImagePath);
                        if (!CanTouchUi())
                        {
                            return;
                        }
                    }
                    else
                    {
                        Logging.Write($"Slideshow image found in file cache: {localImagePath}", 0);
                    }

                    if (!CanTouchUi())
                    {
                        return;
                    }

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
            if (!CanTouchUi())
            {
                return;
            }

            cachedActivityContents = guidance.activity?.contents ?? new List<ContentItem>();
            cachedNoticeContents = guidance.notice?.contents ?? new List<ContentItem>();
            cachedNewsContents = guidance.news?.contents ?? new List<ContentItem>();

            NotificationDataHolder.ActivityContents = cachedActivityContents;
            NotificationDataHolder.NoticeContents = cachedNoticeContents;
            NotificationDataHolder.NewsContents = cachedNewsContents;

            NotifyLoad.Visibility = Visibility.Collapsed;
            Notify_Grid.Visibility = Visibility.Visible;
            NotifyNav.Visibility = Visibility.Visible;

            SelectFirstAvailableNotifyItem();
        }

        private void SelectFirstAvailableNotifyItem()
        {
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

            string tempPath = destinationPath + ".tmp";

            try
            {
                using (HttpResponseMessage response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    using (Stream contentStream = await response.Content.ReadAsStreamAsync())
                    using (FileStream fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        await contentStream.CopyToAsync(fileStream);
                    }
                }

                File.Move(tempPath, destinationPath, true);
                Logging.Write($"File downloaded successfully: {destinationPath}", 0);
            }
            catch (Exception ex)
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch { }

                Logging.Write($"Failed to download file: {ex.Message}", 2);
                throw;
            }
        }

        private void Notify_NavView_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            if (sender.SelectedItem is SelectorBarItem selectedItem && selectedItem.Tag is string tag)
            {
                var transitionInfo = new SuppressNavigationTransitionInfo();

                switch (tag)
                {
                    case "Notify_Gallery":
                        var navData = new GalleryNavigationData
                        {
                            Pictures = Pictures,
                            JumpUrls = list
                        };
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
                NotificationManager.RaiseNotification(
                    "游戏选择无效",
                    "选择正确的Wuthering Waves.exe\n通常位于[游戏根目录\\Wuthering Waves Game\\Wuthering Waves.exe]",
                    InfoBarSeverity.Error);
            }
        }

        private void StartGame_Click(object sender, RoutedEventArgs e)
        {
            new GameStartUtil().StartGame();
        }

        private void StartLauncher_Click(object sender, RoutedEventArgs e)
        {
            new GameStartUtil().StartLauncher();
        }

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

        public static async Task PrepareCacheForDataRootMoveAsync()
        {
            isPreparingDataRootMove = true;

            try
            {
                if (CurrentInstance != null && CurrentInstance.TryGetTarget(out MainView mainView))
                {
                    if (mainView.dispatcherQueue != null)
                    {
                        TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

                        bool queued = mainView.dispatcherQueue.TryEnqueue(() =>
                        {
                            try
                            {
                                mainView.ReleaseCacheResourcesForDataRootMove();
                                tcs.TrySetResult(true);
                            }
                            catch (Exception ex)
                            {
                                Logging.Write($"Prepare cache for data root move failed: {ex.Message}", 2);
                                tcs.TrySetResult(false);
                            }
                        });

                        if (queued)
                        {
                            await tcs.Task;
                        }
                    }
                    else
                    {
                        mainView.ReleaseCacheResourcesForDataRootMove();
                    }
                }

                homeLoadedInCurrentProcess = false;
                cachedBackgroundVideoPath = null;
                cachedSloganImagePath = null;
                cachedPicturePaths.Clear();
                cachedJumpUrls.Clear();

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                await Task.Delay(800);
            }
            finally
            {
                isPreparingDataRootMove = false;
            }
        }

        private void ReleaseCacheResourcesForDataRootMove()
        {
            try
            {
                Logging.Write("Releasing MainView cache resources for data root move", 0);

                try
                {
                    App.MainWindow?.ReleaseHomeBackgroundVideo();
                }
                catch (Exception ex)
                {
                    Logging.Write($"Release home background video failed: {ex.Message}", 1);
                }

                try
                {
                    IconImageBrush.ImageSource = null;
                }
                catch (Exception ex)
                {
                    Logging.Write($"Release slogan image failed: {ex.Message}", 1);
                }

                try
                {
                    NotifyFrame.Content = null;
                }
                catch (Exception ex)
                {
                    Logging.Write($"Release notify frame failed: {ex.Message}", 1);
                }

                try
                {
                    Pictures.Clear();
                    list.Clear();
                }
                catch (Exception ex)
                {
                    Logging.Write($"Clear home picture collections failed: {ex.Message}", 1);
                }
            }
            catch (Exception ex)
            {
                Logging.Write($"ReleaseCacheResourcesForDataRootMove failed: {ex.Message}", 2);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            isPageUnloaded = true;

            try
            {
                App.MainWindow?.SetHomeBackgroundVideoMode(false);
            }
            catch (Exception ex)
            {
                Logging.Write($"Hide home background video failed: {ex.Message}", 1);
            }

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
        }
    }
}
