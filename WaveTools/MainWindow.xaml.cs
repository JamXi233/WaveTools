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

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using Vanara.PInvoke;
using Windows.Graphics;
using WinRT.Interop;
using Microsoft.UI.Xaml.Controls;
using WaveTools.Depend;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Microsoft.UI.Xaml.Media.Imaging;
using WaveTools.Views.FirstRunViews;
using static WaveTools.App;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.ApplicationModel;
using Windows.Media.Core;

namespace WaveTools
{
    public partial class MainWindow : Window
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private IntPtr hwnd = IntPtr.Zero;
        private OverlappedPresenter presenter;
        private AppWindow appWindow;
        private AppWindowTitleBar titleBar;
        string ExpectionFileName;
        private bool isClosing;
        private DispatcherTimer firstRunTimer;
        private string currentHomeBackgroundVideoPath;
        private bool isHomeBackgroundVideoClearMode;

        public static bool isWindowOpen = true;

        private const int Width = 1024;
        private const int Height = 584;

        private const int GWL_STYLE = -16;
        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const int WM_NCLBUTTONDBLCLK = 0x00A3;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeConsole();
        [DllImport("User32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private WndProcDelegate newWndProc;
        private IntPtr oldWndProc;


        private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_NCLBUTTONDBLCLK)
            {
                return IntPtr.Zero;
            }
            return CallWindowProc(oldWndProc, hWnd, msg, wParam, lParam);
        }

        public NavigationView NavigationView { get; }

        private Action buttonAction;
        private static bool isDialogOpen = false;

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
            InitStatus();
            CleanUpdate();
            if (AppDataController.GetAutoCheckUpdate() == 1)
            {
                await AutoGetUpdate();
            }
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
            Logo_Progress.Visibility = Visibility.Visible;
            Logo.Visibility = Visibility.Collapsed;
            MainNavigationView.Visibility = Visibility.Visible;

            if (AppDataController.GetFirstRun() != 0)
            {
                FirstRun_Frame.Navigate(typeof(FirstRunAnimation));
                await Task.Delay(1000);
                InitFirstRun();
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
                    FirstRun_Frame.Navigate(typeof(FirstRunRestore));
                    break;
                case 3:
                    FirstRun_Frame.Navigate(typeof(FirstRunTheme));
                    break;
                case 4:
                    FirstRun_Frame.Navigate(typeof(FirstRunSourceSelect));
                    break;
                case 5:
                    FirstRun_Frame.Navigate(typeof(FirstRunGetDepend));
                    break;
                case 6:
                    FirstRun_Frame.Navigate(typeof(FirstRunExtra));
                    break;
                default:
                    Logging.Write($"Unknown FirstRunStatus: {firstRunStatus}", 2);
                    FirstRun_Frame.Navigate(typeof(FirstRunInit));
                    break;
            }
        }

        private void InitStatus()
        {
            string currentVersion = CommonHelpers.AppVersionHelper.GetAppVersion();
            List<string> statusParts = new List<string>
            {
                currentVersion
            };

            if (CommonHelpers.AppVersionHelper.IsPortable())
            {
                statusParts.Add("Portable");
            }

            bool isRunAsAdmin = ProcessRun.IsRunAsAdmin();

            if (isRunAsAdmin)
            {
                statusParts.Add("Privileged");
            }

            AppTitleBar_Status.Visibility = Visibility.Visible;

            if (AppDataController.GetAdminMode() == 1)
            {
                if (!isRunAsAdmin)
                {
                    NotificationManager.RaiseNotification("获取管理员权限时出现问题", "您在设置中开启了\n[使用管理员身份运行]\n\n但WaveTools并没有正确获取到管理员权限", InfoBarSeverity.Warning);
                    statusParts.Add("Refusal");
                }
            }

            if (Debugger.IsAttached || App.SDebugMode)
            {
                statusParts.Add("Debug");
            }

            AppTitleBar_Status.Text = string.Join(" ", statusParts);
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

        private async Task AutoGetUpdate()
        {

            var result = await GetUpdate.GetDependUpdate();
            var status = result.Status;
            if (status == 1)
            {
                NotificationManager.RaiseNotification("更新提示", "依赖包需要更新\n请尽快到[设置-检查依赖更新]进行更新", InfoBarSeverity.Warning, false, 5);
            }
            result = await GetUpdate.GetWaveToolsUpdate();
            status = result.Status;
            if (status == 1)
            {
                NotificationManager.RaiseNotification("更新提示", "WaveTools有更新\n可到[设置-检查更新]进行更新", InfoBarSeverity.Warning, false, 5);
            }
        }

        public void StartCheckingFirstRun()
        {
            if (isClosing)
            {
                return;
            }

            firstRunTimer?.Stop();
            firstRunTimer = new DispatcherTimer();
            firstRunTimer.Interval = TimeSpan.FromSeconds(0.1);
            firstRunTimer.Tick += Timer_Tick;
            firstRunTimer.Start();
        }

        private void Timer_Tick(object sender, object e)
        {
            if (isClosing)
            {
                (sender as DispatcherTimer)?.Stop();
                return;
            }

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

            int style = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, style & ~WS_MAXIMIZEBOX);

            newWndProc = new WndProcDelegate(CustomWndProc);
            oldWndProc = SetWindowLongPtr(hwnd, -4, Marshal.GetFunctionPointerForDelegate(newWndProc));

            float scale = (float)User32.GetDpiForWindow(hwnd) / 96;

            int windowWidth = (int)(Width * scale);
            int windowHeight = (int)(Height * scale);

            Logging.Write("MoveAndResize to " + windowWidth + "*" + windowHeight, 0);
            appWindow.Resize(new SizeInt32(windowWidth, windowHeight));

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                titleBar = appWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                titleBar.IconShowOptions = IconShowOptions.ShowIconAndSystemMenu;
                titleBar.PreferredHeightOption = (TitleBarHeightOption)1;

                if (AppDataController.GetDayNight() == 0)
                {
                    if (App.CurrentTheme == ApplicationTheme.Light) titleBar.ButtonForegroundColor = Colors.Black;
                    else titleBar.ButtonForegroundColor = Colors.White;
                }
                else
                {
                    if (AppDataController.GetDayNight() == 1) titleBar.ButtonForegroundColor = Colors.Black;
                    else titleBar.ButtonForegroundColor = Colors.White;
                }

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
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (isClosing || appWindow == null) return;

                    ApplyThemeNow(0);
                });
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

        public void ApplyThemeNow(int dayNight)
        {
            try
            {
                ElementTheme elementTheme = ElementTheme.Default;

                if (dayNight == 1)
                {
                    elementTheme = ElementTheme.Light;
                    App.SetCurrentTheme(ApplicationTheme.Light);
                }
                else if (dayNight == 2)
                {
                    elementTheme = ElementTheme.Dark;
                    App.SetCurrentTheme(ApplicationTheme.Dark);
                }
                else
                {
                    UpdateCurrentThemeFromSystem();
                    elementTheme = App.CurrentTheme == ApplicationTheme.Light ? ElementTheme.Light : ElementTheme.Dark;
                }

                if (Content is FrameworkElement root)
                {
                    root.RequestedTheme = elementTheme;
                }

                if (appWindow != null)
                {
                    UpdateTitleBarColor(appWindow.TitleBar);
                }

                UpdateHomeBackgroundVideoCoverForTheme();

                Logging.Write("Theme applied without restart: " + dayNight, 0);
            }
            catch (Exception ex)
            {
                Logging.Write("Apply theme without restart failed: " + ex.Message, 2);
                NotificationManager.RaiseNotification("主题切换失败", ex.Message, InfoBarSeverity.Error);
            }
        }

        private void UpdateCurrentThemeFromSystem()
        {
            try
            {
                Windows.UI.ViewManagement.UISettings uiSettings = new Windows.UI.ViewManagement.UISettings();
                Windows.UI.Color foregroundColor = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Foreground);

                if (foregroundColor.R < 128 && foregroundColor.G < 128 && foregroundColor.B < 128)
                {
                    App.SetCurrentTheme(ApplicationTheme.Light);
                }
                else
                {
                    App.SetCurrentTheme(ApplicationTheme.Dark);
                }
            }
            catch
            {
            }
        }

        private void UpdateHomeBackgroundVideoCoverForTheme()
        {
            try
            {
                if (AppDataController.GetDayNight() == 0)
                {
                    UpdateCurrentThemeFromSystem();
                }

                HomeBackgroundVideoBlurLayer.Background = App.CurrentTheme == ApplicationTheme.Light
                    ? new SolidColorBrush(ColorHelper.FromArgb(232, 242, 246, 250))
                    : new SolidColorBrush(ColorHelper.FromArgb(238, 30, 30, 30));
            }
            catch (Exception ex)
            {
                Logging.Write("Update home background video cover failed: " + ex.Message, 1);
            }
        }


        private void DisableWindowResize()
        {
            int style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_STYLE);
            style &= ~NativeMethods.WS_SIZEBOX;
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_STYLE, style);
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
            Exception exception = null;

            try
            {
                e.UnhandledError.Propagate();
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            if (exception == null)
            {
                return;
            }

            try
            {
                ExpectionFileName = string.Format("WaveTools_Panic_{0:yyyyMMdd_HHmmss_fff}.WaveToolsPanic", DateTime.Now);
                string panicText = "源:" + exception.Source + "\n错误标题:" + exception.Message + "\n堆栈跟踪:\n" + exception.StackTrace + "\n内部异常:\n" + exception.InnerException + "\n结束代码:" + exception.HResult + "\n完整错误:\n" + exception;

                try
                {
                    Spectre.Console.AnsiConsole.WriteException(exception, Spectre.Console.ExceptionFormats.ShortenPaths | Spectre.Console.ExceptionFormats.ShortenTypes | Spectre.Console.ExceptionFormats.ShortenMethods | Spectre.Console.ExceptionFormats.ShowLinks | Spectre.Console.ExceptionFormats.Default);
                }
                catch
                {
                }

                try
                {
                    await ExceptionSave.Write(panicText, 1, ExpectionFileName);
                }
                catch
                {
                }

                if (isClosing || !isWindowOpen)
                {
                    return;
                }

                string errorMessage;
                InfoBarSeverity severity = InfoBarSeverity.Error;

                if (exception.Message.Contains("SSL"))
                {
                    errorMessage = "网络连接发生错误\n" + exception.Message;
                    severity = InfoBarSeverity.Warning;
                }
                else
                {
                    errorMessage = exception.Message.Trim() + "\n\n已生成错误报告\n如再次尝试仍会重现错误\n您可以到Github提交Issue";
                }

                AddNotification("严重错误", errorMessage, severity, true, 0, () =>
                {
                    ExpectionFolderOpen_Click();
                }, "打开文件夹");
            }
            catch
            {
            }
        }


        private void ExpectionFolderOpen_Click()
        {
            try
            {
                string folderPath = AppDataController.GetDataPath("Panic");
                Directory.CreateDirectory(folderPath);

                if (!string.IsNullOrWhiteSpace(ExpectionFileName))
                {
                    string filePath = Path.Combine(folderPath, ExpectionFileName);
                    if (!File.Exists(filePath))
                    {
                        using (File.Create(filePath))
                        {
                        }
                    }
                }

                Process.Start("explorer.exe", folderPath);
            }
            catch
            {
            }
        }

        private DateTime lastNotificationTime = DateTime.MinValue;
        private const int ThrottleTimeMilliseconds = 50;

        private bool CanUseNotificationPanel()
        {
            return !isClosing && isWindowOpen && InfoBarPanel != null;
        }

        private List<UIElement> GetNotificationChildrenSnapshot()
        {
            try
            {
                if (!CanUseNotificationPanel())
                {
                    return new List<UIElement>();
                }

                return InfoBarPanel.Children.ToList();
            }
            catch
            {
                return new List<UIElement>();
            }
        }

        private void SafeRemoveInfoBar(InfoBar infoBar)
        {
            try
            {
                if (infoBar == null || !CanUseNotificationPanel())
                {
                    return;
                }

                if (InfoBarPanel.Children.Contains(infoBar))
                {
                    InfoBarPanel.Children.Remove(infoBar);
                }
            }
            catch
            {
            }
        }

        public async void AddNotification(string title, string message, InfoBarSeverity severity, bool isClosable = true, int TimerSec = 0, Action actionButtonAction = null, string actionButtonText = null)
        {
            if (!CanUseNotificationPanel())
            {
                return;
            }

            try
            {
                double maxLength = isClosable ? 25 : 30;

                if (message != null)
                {
                    double currentLength = 0;

                    for (int i = 0; i < message.Length; i++)
                    {
                        if (message[i] == '\n')
                        {
                            currentLength = 0;
                            continue;
                        }

                        if (char.IsDigit(message[i]) || (message[i] >= 'a' && message[i] <= 'z') || (message[i] >= 'A' && message[i] <= 'Z') || char.IsWhiteSpace(message[i]))
                        {
                            currentLength += 1;
                        }
                        else if (char.IsPunctuation(message[i]) || char.IsSymbol(message[i]))
                        {
                            currentLength += 1;
                        }
                        else if (message[i] >= 0x4e00 && message[i] <= 0x9fff)
                        {
                            currentLength += 2;
                        }
                        else
                        {
                            currentLength += 1;
                        }

                        if (currentLength >= maxLength)
                        {
                            message = message.Insert(i + 1, "\n");
                            currentLength = 0;
                            i++;
                        }
                    }
                }

                DateTime currentTime = DateTime.Now;

                if ((currentTime - lastNotificationTime).TotalMilliseconds < ThrottleTimeMilliseconds)
                {
                    await Task.Delay(ThrottleTimeMilliseconds);
                }

                if (!CanUseNotificationPanel())
                {
                    return;
                }

                lastNotificationTime = DateTime.Now;

                if (IsNotificationPresent(message))
                {
                    Logging.Write($"Notification with title '{title}' already present, skipping.", 1);
                    return;
                }

                Logging.WriteNotification(title, message, (int)severity);

                InfoBar infoBar = new InfoBar
                {
                    Title = title,
                    Message = message,
                    Severity = severity,
                    IsOpen = true,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 0, 0, 8),
                    Opacity = 0,
                    RenderTransform = new TranslateTransform(),
                    IsClosable = isClosable,
                    Background = GetNotificationBackgroundBrush(severity),
                    BorderThickness = new Thickness(0)
                };

                if (actionButtonText != null)
                {
                    Button actionButton = new Button { Content = actionButtonText };

                    if (actionButtonAction != null)
                    {
                        actionButton.Click += (sender, args) => actionButtonAction.Invoke();
                    }

                    infoBar.ActionButton = actionButton;
                }

                infoBar.CloseButtonClick += (sender, args) =>
                {
                    SafeRemoveInfoBar(infoBar);
                };

                Storyboard moveDownStoryboard = new Storyboard();

                foreach (UIElement child in GetNotificationChildrenSnapshot())
                {
                    TranslateTransform transform = new TranslateTransform();
                    child.RenderTransform = transform;

                    DoubleAnimation moveDownAnimation = new DoubleAnimation
                    {
                        Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                        From = -20,
                        To = infoBar.ActualHeight,
                        EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut }
                    };

                    Storyboard.SetTarget(moveDownAnimation, child);
                    Storyboard.SetTargetProperty(moveDownAnimation, "(UIElement.RenderTransform).(TranslateTransform.Y)");
                    moveDownStoryboard.Children.Add(moveDownAnimation);
                }

                if (!CanUseNotificationPanel())
                {
                    return;
                }

                InfoBarPanel.Children.Insert(0, infoBar);

                Storyboard flyInStoryboard = new Storyboard();

                DoubleAnimation translateInAnimation = new DoubleAnimation
                {
                    Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                    From = 40,
                    To = 0,
                    EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut }
                };

                Storyboard.SetTarget(translateInAnimation, infoBar);
                Storyboard.SetTargetProperty(translateInAnimation, "(UIElement.RenderTransform).(TranslateTransform.X)");

                DoubleAnimation fadeInAnimation = new DoubleAnimation
                {
                    Duration = new Duration(TimeSpan.FromSeconds(0.5)),
                    From = 0,
                    To = 1,
                    EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut }
                };

                Storyboard.SetTarget(fadeInAnimation, infoBar);
                Storyboard.SetTargetProperty(fadeInAnimation, "Opacity");

                moveDownStoryboard.Begin();
                flyInStoryboard.Children.Add(translateInAnimation);
                flyInStoryboard.Children.Add(fadeInAnimation);
                flyInStoryboard.Begin();

                if (TimerSec > 0)
                {
                    await Task.Delay(10);

                    ProgressBar progressBar = new ProgressBar
                    {
                        Width = 300,
                        Height = 2,
                        IsIndeterminate = false,
                        Margin = new Thickness(-48, -4, 0, 0),
                        Maximum = 100,
                        Value = 100,
                        Foreground = GetNotificationProgressBrush(severity)
                    };

                    if (isClosable)
                    {
                        progressBar.Margin = new Thickness(-54, -4, -48, 0);
                    }

                    infoBar.Content = progressBar;

                    DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TimerSec * 10) };
                    timer.Tick += (sender, args) =>
                    {
                        try
                        {
                            if (!CanUseNotificationPanel())
                            {
                                timer.Stop();
                                return;
                            }

                            if (progressBar.Value > 0)
                            {
                                progressBar.Value -= 1;
                                return;
                            }

                            timer.Stop();
                            Storyboard closeStoryboard = new Storyboard();

                            DoubleAnimation translateOutAnimation = new DoubleAnimation
                            {
                                Duration = new Duration(TimeSpan.FromSeconds(0.3)),
                                From = 0,
                                To = 40,
                                EasingFunction = new BackEase { EasingMode = EasingMode.EaseIn }
                            };

                            Storyboard.SetTarget(translateOutAnimation, infoBar);
                            Storyboard.SetTargetProperty(translateOutAnimation, "(UIElement.RenderTransform).(TranslateTransform.X)");

                            DoubleAnimation fadeOutAnimation = new DoubleAnimation
                            {
                                Duration = new Duration(TimeSpan.FromSeconds(0.3)),
                                From = 1,
                                To = 0,
                                EasingFunction = new BackEase { EasingMode = EasingMode.EaseIn }
                            };

                            Storyboard.SetTarget(fadeOutAnimation, infoBar);
                            Storyboard.SetTargetProperty(fadeOutAnimation, "Opacity");
                            closeStoryboard.Children.Add(translateOutAnimation);
                            closeStoryboard.Children.Add(fadeOutAnimation);
                            closeStoryboard.Completed += (sender, args) =>
                            {
                                try
                                {
                                    infoBar.IsOpen = false;
                                }
                                catch
                                {
                                }

                                SafeRemoveInfoBar(infoBar);
                            };
                            closeStoryboard.Begin();
                        }
                        catch
                        {
                            timer.Stop();
                        }
                    };
                    timer.Start();
                }
            }
            catch (COMException)
            {
            }
            catch
            {
            }
        }

        private Brush GetNotificationProgressBrush(InfoBarSeverity severity)
        {
            switch (severity)
            {
                case InfoBarSeverity.Error:
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 196, 43, 28));
                case InfoBarSeverity.Warning:
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 245, 158, 11));
                case InfoBarSeverity.Success:
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 57, 122, 54));
                case InfoBarSeverity.Informational:
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 64, 126, 201));
                default:
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 64, 126, 201));
            }
        }

        private Brush GetNotificationBackgroundBrush(InfoBarSeverity severity)
        {
            bool isDark = IsDarkTheme();

            switch (severity)
            {
                case InfoBarSeverity.Error:
                    return new SolidColorBrush(isDark ? ColorHelper.FromArgb(250, 72, 30, 30) : ColorHelper.FromArgb(255, 253, 237, 235));
                case InfoBarSeverity.Warning:
                    return new SolidColorBrush(isDark ? ColorHelper.FromArgb(250, 66, 52, 22) : ColorHelper.FromArgb(255, 255, 244, 206));
                case InfoBarSeverity.Success:
                    return new SolidColorBrush(isDark ? ColorHelper.FromArgb(250, 28, 58, 35) : ColorHelper.FromArgb(255, 235, 248, 238));
                case InfoBarSeverity.Informational:
                    return new SolidColorBrush(isDark ? ColorHelper.FromArgb(250, 28, 45, 70) : ColorHelper.FromArgb(255, 237, 246, 253));
                default:
                    return new SolidColorBrush(isDark ? ColorHelper.FromArgb(250, 28, 45, 70) : ColorHelper.FromArgb(255, 237, 246, 253));
            }
        }

        private Brush GetNotificationBorderBrush(InfoBarSeverity severity)
        {
            bool isDark = IsDarkTheme();

            switch (severity)
            {
                case InfoBarSeverity.Error:
                    return new SolidColorBrush(isDark ? ColorHelper.FromArgb(255, 180, 70, 64) : ColorHelper.FromArgb(255, 196, 43, 28));
                case InfoBarSeverity.Warning:
                    return new SolidColorBrush(isDark ? ColorHelper.FromArgb(255, 215, 145, 45) : ColorHelper.FromArgb(255, 196, 127, 0));
                case InfoBarSeverity.Success:
                    return new SolidColorBrush(isDark ? ColorHelper.FromArgb(255, 88, 148, 91) : ColorHelper.FromArgb(255, 57, 122, 54));
                case InfoBarSeverity.Informational:
                    return new SolidColorBrush(isDark ? ColorHelper.FromArgb(255, 72, 128, 190) : ColorHelper.FromArgb(255, 64, 126, 201));
                default:
                    return new SolidColorBrush(isDark ? ColorHelper.FromArgb(255, 72, 128, 190) : ColorHelper.FromArgb(255, 64, 126, 201));
            }
        }

        private bool IsDarkTheme()
        {
            try
            {
                FrameworkElement root = Content as FrameworkElement;

                if (root != null)
                {
                    return root.ActualTheme == ElementTheme.Dark;
                }
            }
            catch
            {
            }

            return true;
        }

        public bool IsNotificationPresent(string message)
        {
            try
            {
                foreach (InfoBar infoBar in GetNotificationChildrenSnapshot().OfType<InfoBar>())
                {
                    if (infoBar.Message == message)
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        public void ShowHomeBackgroundVideo(string videoPath)
        {
            if (isClosing || string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                return;
            }

            try
            {
                if (!string.Equals(currentHomeBackgroundVideoPath, videoPath, StringComparison.OrdinalIgnoreCase))
                {
                    HomeBackgroundMediaPlayer.AutoPlay = false;
                    HomeBackgroundMediaPlayer.Source = MediaSource.CreateFromUri(new Uri(videoPath));
                    currentHomeBackgroundVideoPath = videoPath;
                }

                if (HomeBackgroundMediaPlayer.MediaPlayer != null)
                {
                    HomeBackgroundMediaPlayer.MediaPlayer.IsLoopingEnabled = true;
                    HomeBackgroundMediaPlayer.MediaPlayer.Play();
                }

                HomeBackgroundMediaPlayer.Visibility = Visibility.Visible;
                ApplyHomeBackgroundVideoMode(isHomeBackgroundVideoClearMode, true);
            }
            catch (Exception ex)
            {
                Logging.Write("Show home background video failed: " + ex.Message, 2);
            }
        }

        public void SetHomeBackgroundVideoMode(bool isClearMode)
        {
            isHomeBackgroundVideoClearMode = isClearMode;
            ApplyHomeBackgroundVideoMode(isClearMode, true);
        }

        private void ApplyHomeBackgroundVideoMode(bool isClearMode, bool animate)
        {
            if (isClosing || HomeBackgroundMediaPlayer.Source == null)
            {
                return;
            }

            try
            {
                UpdateHomeBackgroundVideoCoverForTheme();
                HomeBackgroundMediaPlayer.Visibility = Visibility.Visible;
                HomeBackgroundVideoBlurLayer.Visibility = Visibility.Visible;

                if (HomeBackgroundMediaPlayer.MediaPlayer != null)
                {
                    HomeBackgroundMediaPlayer.MediaPlayer.Play();
                }

                TimeSpan transitionDuration = TimeSpan.FromMilliseconds(80);
                double videoOpacity = isClearMode ? 1 : 0.16;
                double blurOpacity = isClearMode ? 0 : 1;

                if (animate)
                {
                    AnimateOpacity(HomeBackgroundMediaPlayer, videoOpacity, transitionDuration);
                    AnimateOpacity(HomeBackgroundVideoBlurLayer, blurOpacity, transitionDuration, () =>
                    {
                        if (blurOpacity == 0)
                        {
                            HomeBackgroundVideoBlurLayer.Visibility = Visibility.Collapsed;
                        }
                    });
                }
                else
                {
                    HomeBackgroundMediaPlayer.Opacity = videoOpacity;
                    HomeBackgroundVideoBlurLayer.Opacity = blurOpacity;

                    if (blurOpacity == 0)
                    {
                        HomeBackgroundVideoBlurLayer.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Write("Apply home background video mode failed: " + ex.Message, 1);
            }
        }

        private void AnimateOpacity(UIElement target, double to, TimeSpan duration, Action completed = null)
        {
            DoubleAnimation animation = new DoubleAnimation
            {
                To = to,
                Duration = duration,
                EnableDependentAnimation = true
            };

            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, "Opacity");

            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            if (completed != null)
            {
                storyboard.Completed += (sender, args) => completed();
            }
            storyboard.Begin();
        }

        public void HideHomeBackgroundVideo()
        {
            try
            {
                SetHomeBackgroundVideoMode(false);
            }
            catch (Exception ex)
            {
                Logging.Write("Hide home background video failed: " + ex.Message, 1);
            }
        }

        public void ReleaseHomeBackgroundVideo()
        {
            try
            {
                if (HomeBackgroundMediaPlayer.MediaPlayer != null)
                {
                    HomeBackgroundMediaPlayer.MediaPlayer.Pause();
                }

                HomeBackgroundMediaPlayer.AutoPlay = false;
                HomeBackgroundMediaPlayer.Opacity = 0;
                HomeBackgroundMediaPlayer.Visibility = Visibility.Collapsed;
                HomeBackgroundVideoBlurLayer.Opacity = 0;
                HomeBackgroundVideoBlurLayer.Visibility = Visibility.Collapsed;
                HomeBackgroundMediaPlayer.Source = null;
                currentHomeBackgroundVideoPath = null;
            }
            catch (Exception ex)
            {
                Logging.Write("Release home background video failed: " + ex.Message, 1);
            }
        }

        public async void ShowWaitOverlay(bool status, string title = null, string subtitle = null, bool isProgress = false, int progress = 0, bool isBtnEnabled = false, string btnContent = "", Action btnAction = null)
        {
            if (isClosing)
            {
                return;
            }

            try
            {
                if (status)
                {
                    FadeInStoryboard.Begin();
                    WaitOverlay.Visibility = Visibility.Visible;
                    if (isProgress) { WaitOverlay_Progress_Grid.Visibility = Visibility.Visible; WaitOverlay_Progress.Visibility = Visibility.Visible; }
                    else WaitOverlay_Progress_Grid.Visibility = Visibility.Collapsed;
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
                    FadeOutStoryboard.Begin();
                    await Task.Delay(100);
                    WaitOverlay.Visibility = Visibility.Collapsed;
                    WaitOverlay_Progress.Visibility = Visibility.Collapsed;
                    WaitOverlay_Success.Visibility = Visibility.Collapsed;
                    WaitOverlay_Button.Visibility = Visibility.Collapsed;
                    WaitOverlay_Button.IsEnabled = false;
                    buttonAction = null;
                }
            }
            catch (COMException)
            {
            }
            catch
            {
            }
        }

        private void WaitOverlayButton_Click(object sender, RoutedEventArgs e)
        {
            buttonAction?.Invoke();
        }

        private async void ShowDialog(XamlRoot xamlRoot, string title = null, object content = null, bool isPrimaryButtonEnabled = false, string primaryButtonContent = "", Action primaryButtonAction = null, bool isSecondaryButtonEnabled = false, string secondaryButtonContent = "", Action secondaryButtonAction = null)
        {
            if (isClosing)
            {
                return;
            }

            if (isDialogOpen)
            {
                return;
            }

            isDialogOpen = true;

            // 如果 content 是 string 类型，创建一个 TextBlock 包装它
            if (content is string textContent)
            {
                content = new TextBlock { Text = textContent, FontSize = 14, TextWrapping = TextWrapping.Wrap };
            }

            ContentDialog dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                Title = title,
                PrimaryButtonText = isPrimaryButtonEnabled ? primaryButtonContent : null,
                SecondaryButtonText = isSecondaryButtonEnabled ? secondaryButtonContent : null,
                CloseButtonText = "关闭",
                DefaultButton = ContentDialogButton.Primary,
                Content = content
            };

            if (isPrimaryButtonEnabled)
            {
                dialog.PrimaryButtonClick += (sender, args) => primaryButtonAction?.Invoke();
            }

            if (isSecondaryButtonEnabled)
            {
                dialog.SecondaryButtonClick += (sender, args) => secondaryButtonAction?.Invoke();
            }

            try
            {
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                if (!isClosing)
                {
                    NotificationManager.RaiseNotification("对话框出现问题", ex.Message, InfoBarSeverity.Error, true, 5);
                }
            }
            finally
            {
                isDialogOpen = false;
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs e)
        {
            isClosing = true;
            isWindowOpen = false;

            firstRunTimer?.Stop();
            firstRunTimer = null;

            ReleaseHomeBackgroundVideo();

            Windows.ApplicationModel.Core.CoreApplication.UnhandledErrorDetected -= OnUnhandledErrorDetected;
            NotificationManager.OnNotificationRequested -= AddNotification;
            WaitOverlayManager.OnWaitOverlayRequested -= ShowWaitOverlay;
            DialogManager.OnDialogRequested -= ShowDialog;
        }

    }
}
