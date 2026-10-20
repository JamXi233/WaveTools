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
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using WaveTools.Depend;
using WaveTools.Views.SGViews;
using WaveTools.Views.ToolViews;
using Windows.Foundation;
using static WaveTools.App;

namespace WaveTools.Views
{
    public sealed partial class StartGameView : Page
    {
        private DispatcherQueue dispatcherQueue;
        private DispatcherQueueTimer dispatcherTimer_Game;
        private DispatcherQueueTimer dispatcherTimer_Launcher;

        private bool isViewUnloaded;
        private int loadVersion;
        private bool lastGameRunning;
        private bool lastLauncherRunning;

        public static string GS = null;
        public static string SelectedUID = null;
        public static string SelectedName = null;

        public StartGameView()
        {
            this.InitializeComponent();
            Logging.Write("Switch to StartGameView", 0);
            this.Loaded += StartGameView_Loaded;
            this.Unloaded += OnUnloaded;

            // 获取UI线程的DispatcherQueue
            InitializeDispatcherQueue();
            // 初始化并启动定时器
            InitializeTimers();
            ApplyCurrentProcessState();

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

        private async void StartGameView_Loaded(object sender, RoutedEventArgs e)
        {
            isViewUnloaded = false;
            ApplyCurrentProcessState();

            LoadDataAsync();
            ApplyCurrentProcessState();

            await GetPromptAsync();
        }

        private bool HasValidGamePath()
        {
            string gamePath = AppDataController.GetGamePath();
            return !string.IsNullOrEmpty(gamePath) && !gamePath.Contains("Null");
        }

        private bool IsGameProcessRunning()
        {
            return Process.GetProcessesByName("Wuthering Waves").Length > 0;
        }

        private bool IsLauncherProcessRunning()
        {
            return Process.GetProcessesByName("launcher").Length > 0;
        }

        private bool IsCurrentViewActive(int version)
        {
            return !isViewUnloaded && version == loadVersion;
        }

        private void ApplyCurrentProcessState()
        {
            if (isViewUnloaded) return;

            ApplyGameProcessState(IsGameProcessRunning());
            ApplyLauncherProcessState(IsLauncherProcessRunning());
        }

        private void ApplyGameProcessState(bool isRunning)
        {
            lastGameRunning = isRunning;
            resourceQualitySelector.IsEnabled = !isRunning;

            if (isRunning)
            {
                startGame.Visibility = Visibility.Collapsed;
                gameRunning.Visibility = Visibility.Visible;

                Frame_GraphicSettingView_Loading.Visibility = Visibility.Collapsed;
                Frame_AccountView_Loading.Visibility = Visibility.Collapsed;

                Frame_GraphicSettingView_Launched_Disable.Visibility = Visibility.Visible;
                Frame_GraphicSettingView_Launched_Disable_Title.Text = "鸣潮正在运行";
                Frame_GraphicSettingView_Launched_Disable_Subtitle.Text = "游戏运行时无法修改画质";

                Frame_AccountView_Launched_Disable.Visibility = Visibility.Visible;
                Frame_AccountView_Launched_Disable_Title.Text = "鸣潮正在运行";
                Frame_AccountView_Launched_Disable_Subtitle.Text = "游戏运行时无法切换账号";
            }
            else
            {
                startGame.Visibility = Visibility.Visible;
                gameRunning.Visibility = Visibility.Collapsed;

                Frame_GraphicSettingView_Launched_Disable.Visibility = Visibility.Collapsed;
                Frame_AccountView_Launched_Disable.Visibility = Visibility.Collapsed;
            }
        }

        private void ApplyLauncherProcessState(bool isRunning)
        {
            lastLauncherRunning = isRunning;

            startLauncher.Visibility = isRunning ? Visibility.Collapsed : Visibility.Visible;
            launcherRunning.Visibility = isRunning ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LoadDataAsync(string mode = null)
        {
            if (isViewUnloaded) return;

            int currentLoadVersion = ++loadVersion;

            if (!HasValidGamePath())
            {
                UpdateUIElementsVisibility(0);
                ApplyCurrentProcessState();
                return;
            }

            string GamePath = AppDataController.GetGamePath();
            Logging.Write("GamePath: " + GamePath, 0);

            UpdateUIElementsVisibility(1);
            CheckIsWeGameVersion(false);

            if (AppDataController.GetDX11Enable() == 1)
            {
                dx11Enable.IsChecked = true;
            }

            ApplyCurrentProcessState();
            if (IsGameProcessRunning())
            {
                return;
            }

            if (mode == null)
            {
                CheckProcess_Account(currentLoadVersion);
                CheckProcess_Graphics(currentLoadVersion);
            }
            else if (mode == "Graphics")
            {
                CheckProcess_Graphics(currentLoadVersion);
            }
            else if (mode == "Account")
            {
                CheckProcess_Account(currentLoadVersion);
            }
        }

        private async void SelectGame(object sender, RoutedEventArgs e)
        {
            string filePath = await CommonHelpers.FileHelpers.OpenFile(".exe");
            if (filePath != null && filePath.Contains("Wuthering Waves.exe"))
            {
                AppDataController.SetGamePath(filePath);

                int currentLoadVersion = ++loadVersion;

                UpdateUIElementsVisibility(1);
                CheckIsWeGameVersion(true);
                ApplyCurrentProcessState();

                if (!IsGameProcessRunning())
                {
                    CheckProcess_Graphics(currentLoadVersion);
                    CheckProcess_Account(currentLoadVersion);
                }
            }
            else
            {
                ValidGameFile.Subtitle = "选择正确的Wuthering Waves.exe\n通常位于[游戏根目录\\Wuthering Waves Game\\Wuthering Waves.exe]";
                ValidGameFile.IsOpen = true;
            }
        }

        private void GamePathAction_Click(object sender, RoutedEventArgs e)
        {
            string gamePath = AppDataController.GetGamePath();

            if (string.IsNullOrEmpty(gamePath) || gamePath.Contains("Null"))
            {
                SelectGame(sender, e);
            }
            else
            {
                RMGameLocation(sender, e);
            }
        }

        private bool CheckIsWeGameVersion(bool isFirst)
        {
            if (Directory.Exists(AppDataController.GetGamePathWithoutGameName() + "Client\\Binaries\\Win64\\ThirdParty\\KrPcSdk_Mainland\\KRSDKRes\\wegame"))
            {
                if (isFirst) NotificationManager.RaiseNotification("检测到WeGame版本", "游戏将无法从WaveTools启动\n无法账号切换", InfoBarSeverity.Warning, true, 5);
                startGame.IsEnabled = false;
                startLauncher.IsEnabled = false;
                Frame_AccountView_Disable.Visibility = Visibility.Visible;
                Frame_AccountView_Disable_Title.Text = "检测到WeGame版鸣潮";
                Frame_AccountView_Disable_Subtitle.Text = "无法进行账号切换";
                return true;
            }
            else Frame_AccountView_Disable.Visibility = Visibility.Collapsed;
            return false;
        }

        public void AdvancedSettings(object sender, RoutedEventArgs e)
        {
            StackPanel advancedPanel = new StackPanel();
            advancedPanel.Children.Add(new TextBlock
            {
                Text = "游戏启动参数",
                Margin = new Thickness(0, 0, 0, 8)
            });
            TextBox gameArgs = new TextBox();
            gameArgs.Text = AppDataController.GetGameParameter();
            advancedPanel.Children.Add(gameArgs);
            DialogManager.RaiseDialog(XamlRoot, "高级设置", advancedPanel, true, "保存", () =>
            {
                string requested = GameResourceQuality.GetArgumentQuality(gameArgs.Text);
                if (requested != null)
                {
                    string gamePath = AppDataController.GetGamePath();
                    var installation = GameResourceQuality.Detect(gamePath);
                    if (installation.RequiresArgument && !System.Linq.Enumerable.Any(installation.Options, option => option.Code == requested))
                    {
                        NotificationManager.RaiseNotification("未安装该画质", "请先在官方启动器下载对应画质资源。", InfoBarSeverity.Warning, false, 5);
                        return;
                    }
                    if (System.Linq.Enumerable.Any(installation.Options, option => option.Code == requested))
                        AppDataController.SetGameResourceQuality(gamePath, requested);
                }
                AppDataController.SetGameParameter(gameArgs.Text);
                _ = resourceQualitySelector.RefreshAsync();
            });
        }

        public void dx11Enable_Toggle(object sender, RoutedEventArgs e)
        {
            AppDataController.SetDX11Enable(dx11Enable.IsChecked == true ? 1 : 0);
        }

        public void RMGameLocation(object sender, RoutedEventArgs e)
        {
            AppDataController.RMGamePath();
            UpdateUIElementsVisibility(0);
        }
        private void ReloadFrame(object sender, RoutedEventArgs e)
        {
            StartGameView_Loaded(sender, e);
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

        private void UpdateUIElementsVisibility(int status)
        {
            resourceQualitySelector.SetGamePath(status == 0 ? null : AppDataController.GetGamePath());
            if (status == 0)
            {
                gamePathAction.IsEnabled = true;
                gamePathActionText.Text = "选择游戏本体";
                gamePathActionIcon.Glyph = "\uE8E5";
                UpdateGamePathActionButtonStyle(false);

                advancedSettings.Visibility = Visibility.Visible;
                reloadFrame.Visibility = Visibility.Visible;
                dx11Enable.Visibility = Visibility.Visible;

                advancedSettings.IsEnabled = false;
                reloadFrame.IsEnabled = false;
                dx11Enable.IsEnabled = false;

                startGame.IsEnabled = false;
                startLauncher.IsEnabled = false;

                SGFrame.Visibility = Visibility.Visible;

                Frame_GraphicSettingView_NoGamePath_Disable.Visibility = Visibility.Visible;
                Frame_AccountView_NoGamePath_Disable.Visibility = Visibility.Visible;
            }
            else
            {
                gamePathAction.IsEnabled = true;
                gamePathActionText.Text = "清除路径";
                gamePathActionIcon.Glyph = "\uE74D";
                UpdateGamePathActionButtonStyle(true);

                advancedSettings.Visibility = Visibility.Visible;
                reloadFrame.Visibility = Visibility.Visible;
                dx11Enable.Visibility = Visibility.Visible;

                advancedSettings.IsEnabled = true;
                reloadFrame.IsEnabled = true;
                dx11Enable.IsEnabled = true;

                startGame.IsEnabled = true;
                startLauncher.IsEnabled = true;

                SGFrame.Visibility = Visibility.Visible;

                Frame_GraphicSettingView_NoGamePath_Disable.Visibility = Visibility.Collapsed;
                Frame_AccountView_NoGamePath_Disable.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateGamePathActionButtonStyle(bool hasGamePath)
        {
            if (hasGamePath)
            {
                gamePathAction.Resources.Remove("ButtonBackground");
                gamePathAction.Resources.Remove("ButtonBackgroundPointerOver");
                gamePathAction.Resources.Remove("ButtonBackgroundPressed");
                gamePathAction.Resources.Remove("ButtonForeground");
                gamePathAction.Resources.Remove("ButtonForegroundPointerOver");
                gamePathAction.Resources.Remove("ButtonForegroundPressed");
                gamePathAction.Resources.Remove("ButtonBorderBrush");
                gamePathAction.Resources.Remove("ButtonBorderBrushPointerOver");
                gamePathAction.Resources.Remove("ButtonBorderBrushPressed");

                gamePathAction.ClearValue(Button.BackgroundProperty);
                gamePathAction.ClearValue(Button.ForegroundProperty);
                gamePathAction.ClearValue(Button.BorderBrushProperty);
            }
            else
            {
                var normalBlue = new SolidColorBrush(ColorHelper.FromArgb(255, 0, 98, 165));
                var hoverBlue = new SolidColorBrush(ColorHelper.FromArgb(255, 0, 90, 158));
                var pressedBlue = new SolidColorBrush(ColorHelper.FromArgb(255, 0, 69, 120));
                var white = new SolidColorBrush(Colors.White);
                gamePathAction.Background = normalBlue;
                gamePathAction.Foreground = white;
                gamePathAction.BorderBrush = normalBlue;
                gamePathAction.Resources["ButtonBackgroundPointerOver"] = hoverBlue;
                gamePathAction.Resources["ButtonForegroundPointerOver"] = white;
                gamePathAction.Resources["ButtonBorderBrushPointerOver"] = hoverBlue;
                gamePathAction.Resources["ButtonBackgroundPressed"] = pressedBlue;
                gamePathAction.Resources["ButtonForegroundPressed"] = white;
                gamePathAction.Resources["ButtonBorderBrushPressed"] = pressedBlue;
                gamePathAction.Resources["ButtonBackground"] = normalBlue;
                gamePathAction.Resources["ButtonForeground"] = white;
                gamePathAction.Resources["ButtonBorderBrush"] = normalBlue;
            }
        }

        public async void StartGame(TeachingTip sender, object args)
        {
            if (AppDataController.GetAccountChangeMode() == 0 || AppDataController.GetAccountChangeMode() == -1)
            {
                GameStartUtil gameStartUtil = new GameStartUtil();
                gameStartUtil.StartGame();
            }
            else
            {
                if (SelectedUID != null || SelectedName != null)
                {
                    string command = $"/RestoreUser {SelectedUID} {SelectedName}";
                    await ProcessRun.WaveToolsHelperAsync(command);
                    GameStartUtil gameStartUtil = new GameStartUtil();
                    gameStartUtil.StartGame();
                }
                else
                {
                    NoSelectedAccount.IsOpen = true;
                }
            }

        }

        public void StartLauncher(TeachingTip sender, object args)
        {
            GameStartUtil gameStartUtil = new GameStartUtil();
            gameStartUtil.StartLauncher();
        }

        // 定时器回调函数，检查进程是否正在运行
        private void CheckProcess_Game(DispatcherQueueTimer timer, object e)
        {
            if (isViewUnloaded) return;

            bool wasRunning = lastGameRunning;
            bool isRunning = IsGameProcessRunning();

            ApplyGameProcessState(isRunning);

            if (wasRunning && !isRunning && HasValidGamePath())
            {
                LoadDataAsync();
            }
        }

        private void CheckProcess_Launcher(DispatcherQueueTimer timer, object e)
        {
            if (isViewUnloaded) return;
            ApplyLauncherProcessState(IsLauncherProcessRunning());
        }

        private async void CheckProcess_Graphics(int currentLoadVersion)
        {
            if (!IsCurrentViewActive(currentLoadVersion)) return;

            if (IsGameProcessRunning())
            {
                ApplyGameProcessState(true);
                return;
            }

            Frame_GraphicSettingView_Loading.Visibility = Visibility.Visible;
            Frame_GraphicSettingView.Content = null;

            if (IsWaveToolsHelperRequireUpdate)
            {
                if (!IsCurrentViewActive(currentLoadVersion)) return;

                Frame_GraphicSettingView_Loading.Visibility = Visibility.Collapsed;
                Frame_GraphicSettingView_Disable.Visibility = Visibility.Visible;
                Frame_GraphicSettingView_Disable_Title.Text = "WaveToolsHelper需要更新";
                Frame_GraphicSettingView_Disable_Subtitle.Text = "请更新后再使用";
            }
            else
            {
                try
                {
                    string GSValue = await ProcessRun.WaveToolsHelperAsync($"/GetGS {AppDataController.GetGamePathForHelper()}", true);

                    if (!IsCurrentViewActive(currentLoadVersion)) return;

                    if (IsGameProcessRunning())
                    {
                        Frame_GraphicSettingView_Loading.Visibility = Visibility.Collapsed;
                        ApplyGameProcessState(true);
                        return;
                    }

                    JObject graphics = JObject.Parse(GSValue);
                    if (graphics["ImageQuality"] == null)
                    {
                        GS = null;
                        GraphicSelect.IsEnabled = false;
                        GraphicSelect.IsSelected = false;
                        Frame_GraphicSettingView_Loading.Visibility = Visibility.Collapsed;
                        Frame_GraphicSettingView_Disable.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        GS = GSValue;
                        GraphicSelect.IsEnabled = true;
                        GraphicSelect.IsSelected = true;
                        Frame_GraphicSettingView_Loading.Visibility = Visibility.Collapsed;
                        Frame_GraphicSettingView.Visibility = Visibility.Visible;
                        Frame_GraphicSettingView.Navigate(typeof(GraphicSettingView));
                    }
                }
                catch (Exception ex)
                {
                    if (IsCurrentViewActive(currentLoadVersion))
                    {
                        Logging.Write($"Exception in CheckProcess_Graphics: {ex.Message}", 3, "CheckProcess_Graphics");
                        GS = null;
                        Frame_GraphicSettingView_Loading.Visibility = Visibility.Collapsed;
                        Frame_GraphicSettingView_Disable.Visibility = Visibility.Visible;
                        Frame_GraphicSettingView_Disable_Title.Text = "读取画质设置失败";
                        Frame_GraphicSettingView_Disable_Subtitle.Text = "请更新 Helper 后重试";
                    }
                }
            }
        }

        private void CheckProcess_Account(int currentLoadVersion)
        {
            if (!IsCurrentViewActive(currentLoadVersion)) return;

            if (IsGameProcessRunning())
            {
                ApplyGameProcessState(true);
                return;
            }

            if (AppDataController.GetAccountChangeMode() != 1)
            {
                AccountChange_Off_Btn.Visibility = Visibility.Collapsed;
                Frame_AccountView_Usage_Disable.Visibility = Visibility.Visible;
            }
            else
            {
                AccountChange_Off_Btn.Visibility = Visibility.Visible;
                Frame_AccountView_Usage_Disable.Visibility = Visibility.Collapsed;
                Frame_AccountView.Content = null;
                AccountSelect.IsEnabled = true;
                AccountSelect.IsSelected = true;
                Frame_AccountView_Loading.Visibility = Visibility.Collapsed;
                Frame_AccountView.Visibility = Visibility.Visible;
                Frame_AccountView.Navigate(typeof(AccountView));
                CheckIsWeGameVersion(false);
            }
        }

        private void AccountChange_ForceOn(object sender, RoutedEventArgs e)
        {
            AppDataController.SetAccountChangeMode(1);
            LoadDataAsync("Account");
        }

        private void AccountChange_Off(object sender, RoutedEventArgs e)
        {
            AppDataController.SetAccountChangeMode(0);
            LoadDataAsync("Account");
        }

        private void Advanced_Graphics_Click(object sender, RoutedEventArgs e)
        {
            DialogManager.RaiseDialog(XamlRoot, "高级画质设置", new AdvancedGraphicSettingsView());
        }

        private async Task GetPromptAsync()
        {
            try
            {
                HttpClient client = new HttpClient();
                string url = "https://wavetools.jamsg.cn/WaveTools_Prompt";
                HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string jsonData = await response.Content.ReadAsStringAsync();
                    if (isViewUnloaded) return;
                    prompt.Text = jsonData;
                }
            }
            catch (Exception ex)
            {
                Logging.Write($"Error fetching prompt: {ex.Message}", 2);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            isViewUnloaded = true;
            loadVersion++;

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
    }
}
