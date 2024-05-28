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

using WaveTools.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using Vanara.PInvoke;
using Windows.Graphics;
using WinRT.Interop;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using WaveTools.Depend;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Microsoft.UI.Xaml.Media.Imaging;
using Spectre.Console;
using Windows.System;
using Windows.Storage.AccessCache;
using WaveTools.Views.FirstRunViews;
using static WaveTools.App;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using WaveTools.Views.ToolViews;
using System.ComponentModel;
using SRTools.Depend;
using System.Diagnostics;

namespace WaveTools
{
    public partial class MainWindow : Window
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private IntPtr hwnd = IntPtr.Zero;
        private OverlappedPresenter presenter;
        private AppWindow appWindow = null;
        private AppWindowTitleBar titleBar;
        string ExpectionFileName;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeConsole();
        [DllImport("User32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        public NavigationView NavigationView { get; }

        private Action buttonAction;

        private MainFrameController mainFrameController;

        public MainWindow()
        {
            Windows.ApplicationModel.Core.CoreApplication.UnhandledErrorDetected += OnUnhandledErrorDetected;
            Title = "鸣潮工具箱";
            InitShiftPress();
            InitializeWindowProperties();
            InitializeComponent();

            NotificationManager.OnNotificationRequested += AddNotification;
            WaitOverlayManager.OnWaitOverlayRequested += ShowWaitOverlay;
            DialogManager.OnDialogRequested += ShowDialog;
            mainFrameController = new MainFrameController(MainFrame);

            this.Activated += MainWindow_Activated;
            this.Closed += MainWindow_Closed;
        }

        private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            this.Activated -= MainWindow_Activated;
            await InitializeAppDataAsync();
            await LoadBackgroundAsync();
            CleanUpdate();
        }

        private void InitShiftPress()
        {
            bool isShiftPressed = (GetAsyncKeyState(0x10) & 0x8000) != 0;

            if (isShiftPressed)
            {
                Logging.Write("已通过快捷键进入控制台模式", 1);
                Console.Title = "🆂𝐃𝐞𝐛𝐮𝐠𝐌𝐨𝐝𝐞:WaveTools";
                TerminalMode.ShowConsole();
                SDebugMode = true;
            }
            else
            {
                Logging.Write("NoPressed", 1);
            }
        }

        private async Task InitializeAppDataAsync()
        {
            AppDataController appDataController = new AppDataController();

            Logo_Progress.Visibility = Visibility.Visible;
            Logo.Visibility = Visibility.Collapsed;
            MainNavigationView.Visibility = Visibility.Visible;

            if (AppDataController.GetFirstRun() == 1)
            {
                FirstRun_Frame.Navigate(typeof(FirstRunAnimation));
                await Task.Delay(1000);

                if (appDataController.CheckOldData() == 1)
                {
                    FirstRunAnimation.isOldDataExist = true;
                    StartCheckingFirstRun();
                }
                else
                {
                    InitFirstRun();
                }

                MainAPP.Visibility = Visibility.Collapsed;
            }
            else
            {
                KillFirstUI();
            }
        }

        private void InitFirstRun()
        {
            StartCheckingFirstRun();
            Logo_Progress.Visibility = Visibility.Collapsed;
            Logo.Visibility = Visibility.Visible;
            MainNavigationView.Visibility = Visibility.Visible;

            int firstRunStatus = AppDataController.GetFirstRunStatus();

            switch (firstRunStatus)
            {
                case 1:
                case 0:
                case -1:
                    FirstRun_Frame.Navigate(typeof(FirstRunInit));
                    break;
                case 2:
                    FirstRun_Frame.Navigate(typeof(FirstRunTheme));
                    break;
                case 3:
                    FirstRun_Frame.Navigate(typeof(FirstRunSourceSelect));
                    break;
                case 4:
                    FirstRun_Frame.Navigate(typeof(FirstRunGetDepend));
                    break;
                case 5:
                    FirstRun_Frame.Navigate(typeof(FirstRunExtra));
                    break;
                default:
                    Logging.Write($"Unknown FirstRunStatus: {firstRunStatus}", 2);
                    FirstRun_Frame.Navigate(typeof(FirstRunInit));
                    break;
            }
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                mainFrameController.Navigate("settings");
            }
            else if (args.SelectedItemContainer != null)
            {
                string tag = args.SelectedItemContainer.Tag.ToString();
                mainFrameController.Navigate(tag);
            }
        }

        public void StartCheckingFirstRun()
        {
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(0.1);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, object e)
        {
            if (AppDataController.GetFirstRun() == 0)
            {
                KillFirstUI();
                (sender as DispatcherTimer)?.Stop();
            }
        }

        public void KillFirstUI()
        {
            MainNavigationView.Visibility = Visibility.Collapsed;
            MainAPP.Visibility = Visibility.Visible;
        }

        private void InitializeWindowProperties()
        {
            hwnd = WindowNative.GetWindowHandle(this);
            WindowId id = Win32Interop.GetWindowIdFromWindow(hwnd);
            appWindow = AppWindow.GetFromWindowId(id);
            DisableWindowResize();
            presenter = appWindow.Presenter as OverlappedPresenter;
            if (presenter != null)
            { 
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
            }

            float scale = (float)User32.GetDpiForWindow(hwnd) / 96;

            int windowX = (int)(560 * scale);
            int windowY = (int)(280 * scale);
            int windowWidth = (int)(1024 * scale);
            int windowHeight = (int)(584 * scale);

            Logging.Write("MoveAndResize to " + windowWidth + "*" + windowHeight, 0);
            appWindow.MoveAndResize(new RectInt32(windowX, windowY, windowWidth, windowHeight));

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                titleBar = appWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                if (App.CurrentTheme == ApplicationTheme.Light) titleBar.ButtonForegroundColor = Colors.Black;
                else titleBar.ButtonForegroundColor = Colors.White;

                titleBar.SetDragRectangles(new RectInt32[] { new RectInt32((int)(48 * scale), 0, 10000, (int)(48 * scale)) });
                Logging.Write("SetDragRectangles to " + 48 * scale + "*" + 48 * scale, 0);
            }
            else
            {
                ExtendsContentIntoTitleBar = true;
                SetTitleBar(AppTitleBar);
            }

            if (AppDataController.GetDayNight() == 0)
            {
                RegisterSystemThemeChangeEvents(id);
            }

        }

        private void RegisterSystemThemeChangeEvents(WindowId id)
        {
            var uiSettings = new Windows.UI.ViewManagement.UISettings();
            uiSettings.ColorValuesChanged += (sender, args) =>
            {
                if (appWindow == null) return;
                UpdateTitleBarColor(appWindow.TitleBar);
            };
            UpdateTitleBarColor(appWindow.TitleBar);
        }

        private void UpdateTitleBarColor(AppWindowTitleBar titleBar)
        {
            if (titleBar == null) return;
            if (AppDataController.GetDayNight() == 0)
            {
                var uiSettings = new Windows.UI.ViewManagement.UISettings();
                var foregroundColor = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Foreground);
                titleBar.ButtonForegroundColor = foregroundColor;
            }
            else
            {
                titleBar.ButtonForegroundColor = App.CurrentTheme == ApplicationTheme.Light ? Colors.Black : Colors.White;
            }
        }


        private void DisableWindowResize()
        {
            int style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_STYLE);
            style &= ~NativeMethods.WS_SIZEBOX;
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_STYLE, style);
        }

        private async Task LoadBackgroundAsync()
        {
            string apiUrl = "https://prod-cn-alicdn-gamestarter.kurogame.com/pcstarter/prod/starter/10003_Y8xXrXk65DqFHEDgApn3cpK5lfczpFx5/G152/index.json";
            JObject response = await FetchBData(apiUrl);

            string baseDownloadUrl = "https://pcdownload-wangsu.aki-game.com/";
            string zipUrl = baseDownloadUrl + response["animateBackground"]["url"].ToString();
            string newMd5 = response["animateBackground"]["md5"].ToString();

            string targetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"JSG-LLC\WaveTools\Background\");
            Directory.CreateDirectory(targetPath);
            string zipFilePath = Path.Combine(targetPath, "background.zip");

            string backgroundPath = Path.Combine(targetPath, "home_1.jpg");
            string iconPath = Path.Combine(targetPath, "slogan.png");
            string md5FilePath = Path.Combine(targetPath, "md5.txt");
            if (File.Exists(backgroundPath) && File.Exists(iconPath) && File.Exists(md5FilePath))
            {
                string cachedMd5 = await File.ReadAllTextAsync(md5FilePath);
                if (cachedMd5 == newMd5)
                {
                    await LoadAdvertisementDataAsync(backgroundPath, iconPath);
                    return;
                }
            }
            await DownloadFileAsync(zipUrl, zipFilePath);
            ExtractZipFile(zipFilePath, targetPath);
            await File.WriteAllTextAsync(md5FilePath, newMd5);

            await LoadAdvertisementDataAsync(backgroundPath, iconPath);
        }

        private async Task LoadAdvertisementDataAsync(string backgroundPath, string iconPath)
        {
            Logging.Write("LoadAdvertisementData...", 0);
            Logging.Write("Getting Background: " + backgroundPath, 0);
            BitmapImage backgroundImage = new BitmapImage(new Uri(backgroundPath));
            Background.ImageSource = backgroundImage;
        }

        private async Task<JObject> FetchBData(string apiUrl)
        {
            Logging.Write("FetchData:" + apiUrl, 0);
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

        private async Task DownloadFileAsync(string url, string destinationPath)
        {
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
        }

        private void ExtractZipFile(string zipFilePath, string extractPath)
        {
            using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destinationPath = Path.Combine(extractPath, entry.FullName);
                    entry.ExtractToFile(destinationPath, true);
                }
            }
        }

        public static async Task<ApiResponse> FetchData(string url)
        {
            HttpResponseMessage httpResponse = await httpClient.GetAsync(url);
            httpResponse.EnsureSuccessStatusCode();
            string responseBody = await httpResponse.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ApiResponse>(responseBody);
        }

        public class ApiResponse
        {
            public int retcode { get; set; }
            public string message { get; set; }
        }

        private void CleanUpdate() 
        {
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JSG-LLC", "Updates");
            if (Directory.Exists(folderPath)) Directory.Delete(folderPath, true);
        }

        private async void OnUnhandledErrorDetected(object sender, Windows.ApplicationModel.Core.UnhandledErrorDetectedEventArgs e)
        {
            try
            {
                e.UnhandledError.Propagate();
            }
            catch (Exception ex)
            {
                string errorMessage;
                InfoBarSeverity severity = InfoBarSeverity.Error;
                if (ex.Message.Contains("SSL"))
                {
                    errorMessage = "网络连接发生错误\n" + ex.Message;
                    severity = InfoBarSeverity.Warning;
                }
                else
                {
                    errorMessage = ex.Message.Trim() + "\n\n已生成错误报告\n如再次尝试仍会重现错误\n您可以到Github提交Issue";
                }

                ExpectionFileName = string.Format("WaveTools_Panic_{0:yyyyMMdd_HHmmss}.WaveToolsPanic", DateTime.Now);

                // 显示InfoBar通知
                AddNotification("严重错误", errorMessage, severity, () =>
                {
                    ExpectionFolderOpen_Click();
                }, "打开文件夹");

                AnsiConsole.WriteException(ex, ExceptionFormats.ShortenPaths | ExceptionFormats.ShortenTypes | ExceptionFormats.ShortenMethods | ExceptionFormats.ShowLinks | ExceptionFormats.Default);
                await ExceptionSave.Write("源:" + ex.Source + "\n错误标题:" + ex.Message + "\n堆栈跟踪:\n" + ex.StackTrace + "\n内部异常:\n" + ex.InnerException + "\n结束代码:" + ex.HResult + "\n完整错误:\n" + ex.ToString(), 1, ExpectionFileName);
            }
        }


        private void ExpectionFolderOpen_Click()
        {
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JSG-LLC", "Panic");
            Directory.CreateDirectory(folderPath);
            string filePath = Path.Combine(folderPath, ExpectionFileName);
            if (!File.Exists(filePath))
            {
                File.Create(filePath).Dispose();
            }
            Process.Start("explorer.exe", folderPath);
        }


        public void AddNotification(string title, string message, InfoBarSeverity severity, Action actionButtonAction = null, string actionButtonText = null, bool isClosable = true)
        {
            if (IsNotificationPresent(title))
            {
                return;
            }
            InfoBar infoBar = new InfoBar();
            if (isClosable == true)
            {
                infoBar = new InfoBar
                {
                    Title = title,
                    Message = message,
                    Severity = severity,
                    IsOpen = true,
                    VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Top,
                    Margin = new Thickness(0, 0, 0, 5),
                };
            }
            else if(isClosable == false)
            {
                infoBar = new InfoBar
                {
                    Title = title,
                    Message = message,
                    Severity = severity,
                    IsOpen = true,
                    VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Top,
                    Margin = new Thickness(0, 0, 0, 5),
                    IsClosable = false
                };
            }
            if (actionButtonText != null)
            {
                var actionButton = new Button { Content = actionButtonText };
                if (actionButtonAction != null)
                {
                    actionButton.Click += (sender, args) => actionButtonAction.Invoke();
                }
                infoBar.ActionButton = actionButton;
            }
            infoBar.CloseButtonClick += (sender, args) =>
            {
                InfoBarPanel.Children.Remove(sender as InfoBar);
            };
            InfoBarPanel.Children.Add(infoBar);
            int logMode;
            switch (severity)
            {
                case InfoBarSeverity.Informational:
                    logMode = 0; // INFO
                    break;
                case InfoBarSeverity.Success:
                    logMode = 0; // INFO, assuming success also logs as info
                    break;
                case InfoBarSeverity.Warning:
                    logMode = 1; // WARN
                    break;
                case InfoBarSeverity.Error:
                    logMode = 2; // ERROR
                    break;
                default:
                    logMode = 0; // Default to INFO if unspecified
                    break;
            }
            Logging.WriteNotification(title, message, logMode);

        }

        public bool IsNotificationPresent(string title)
        {
            foreach (InfoBar infoBar in InfoBarPanel.Children)
            {
                if ((string)infoBar.Title == title)
                {
                    return true;  // 发现已有相同标题的通知
                }
            }
            return false;  // 没有发现相同标题的通知
        }


        public void ShowWaitOverlay(bool status, bool isProgress = false, int progress = 0, string title = null, string subtitle = null, bool isBtnEnabled = false, string btnContent = "", Action btnAction = null)
        {
            if (status)
            {
                WaitOverlay.Visibility = Visibility.Visible;
                if (isProgress) WaitOverlay_Progress.Visibility = Visibility.Visible;
                else WaitOverlay_Success.Visibility = Visibility.Visible;
                if (progress > 0)
                {
                    WaitOverlay_ProgressBar.Visibility = Visibility.Visible;
                    WaitOverlay_ProgressBar_Value.Visibility = Visibility.Visible;
                    WaitOverlay_ProgressBar.Value = progress;
                    WaitOverlay_ProgressBar_Value.Text = progress.ToString() + "%";
                }
                else
                {
                    WaitOverlay_ProgressBar.Visibility = Visibility.Collapsed;
                    WaitOverlay_ProgressBar_Value.Visibility = Visibility.Collapsed;
                }

                WaitOverlay_Title.Text = title;
                WaitOverlay_SubTitle.Text = subtitle;

                if (isBtnEnabled)
                {
                    WaitOverlay_Button.Visibility = Visibility.Visible;
                    WaitOverlay_Button.IsEnabled = true;
                    buttonAction = btnAction;
                    if (btnContent != "") WaitOverlay_Button.Content = btnContent;
                }
                else
                {
                    WaitOverlay_Button.Visibility = Visibility.Collapsed;
                    WaitOverlay_Button.IsEnabled = false;
                    buttonAction = null;
                }
            }
            else
            {
                WaitOverlay.Visibility = Visibility.Collapsed;
                WaitOverlay_Progress.Visibility = Visibility.Collapsed;
                WaitOverlay_Success.Visibility = Visibility.Collapsed;
                WaitOverlay_Button.Visibility = Visibility.Collapsed;
                WaitOverlay_Button.IsEnabled = false;
                buttonAction = null;
            }
        }

        private void WaitOverlayButton_Click(object sender, RoutedEventArgs e)
        {
            buttonAction?.Invoke();
        }

        private async void ShowDialog(bool status, string title = null, string content = null, bool isPrimaryButtonEnabled = false, string primaryButtonContent = "", Action primaryButtonAction = null, bool isSecondaryButtonEnabled = false, string secondaryButtonContent = "", Action secondaryButtonAction = null)
        {
            if (status)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = title,
                    Content = new TextBlock { Text = content, FontSize = 14 },
                    PrimaryButtonText = isPrimaryButtonEnabled ? primaryButtonContent : null,
                    SecondaryButtonText = isSecondaryButtonEnabled ? secondaryButtonContent : null,
                    CloseButtonText = "关闭",
                    XamlRoot = this.Content.XamlRoot
                };

                if (isPrimaryButtonEnabled)
                {
                    dialog.PrimaryButtonClick += (sender, args) => primaryButtonAction?.Invoke();
                }

                if (isSecondaryButtonEnabled)
                {
                    dialog.SecondaryButtonClick += (sender, args) => secondaryButtonAction?.Invoke();
                }

                await dialog.ShowAsync();
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs e)
        {
            NotificationManager.OnNotificationRequested -= AddNotification;
            WaitOverlayManager.OnWaitOverlayRequested -= ShowWaitOverlay;
            DialogManager.OnDialogRequested -= ShowDialog;
        }

    }
}