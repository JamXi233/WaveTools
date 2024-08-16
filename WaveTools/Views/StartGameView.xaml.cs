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


using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using Microsoft.UI.Dispatching;
using WaveTools.Depend;
using WaveTools.Views.SGViews;
using System.Net.Http;
using System.Threading.Tasks;
using static WaveTools.App;
using Windows.Foundation;
using System.IO;
using WaveTools.Views.ToolViews;

namespace WaveTools.Views
{
    public sealed partial class StartGameView : Page
    {
        private DispatcherQueue dispatcherQueue;
        private DispatcherQueueTimer dispatcherTimer_Game;
        private DispatcherQueueTimer dispatcherTimer_Launcher;

        public static string GS = null;
        public static string SelectedUID = null;
        public static string SelectedName = null;

        public StartGameView()
        {
            this.InitializeComponent();
            Logging.Write("Switch to StartGameView",0);
            this.Loaded += StartGameView_Loaded;
            this.Unloaded += OnUnloaded;

            // 获取UI线程的DispatcherQueue
            InitializeDispatcherQueue();
            // 初始化并启动定时器
            InitializeTimers();

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
            LoadDataAsync();
            await GetPromptAsync();
        }

        private void LoadDataAsync(string mode = null)
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
                    CheckIsWeGameVersion(false);
                    if (mode == null)
                    {
                        CheckProcess_Account();
                        CheckProcess_Graphics();
                    }
                    else if (mode == "Graphics") CheckProcess_Graphics();
                    else if (mode == "Account") CheckProcess_Account();
                }
            }
            else
            {
                UpdateUIElementsVisibility(0);
            }
        }

        private async void SelectGame(object sender, RoutedEventArgs e)
        {
            string filePath = await CommonHelpers.FileHelpers.OpenFile(".exe");
            if (filePath != null && filePath.Contains("Wuthering Waves.exe"))
            {
                // 更新为新的存储管理机制
                AppDataController.SetGamePath(filePath);
                UpdateUIElementsVisibility(1);
                CheckProcess_Graphics();
                CheckProcess_Account();
                CheckIsWeGameVersion(true);
            }
            else
            {
                ValidGameFile.Subtitle = "选择正确的Wuthering Waves.exe\n通常位于[游戏根目录\\Wuthering Waves Game\\Wuthering Waves.exe]";
                ValidGameFile.IsOpen = true;
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
            advancedPanel.Children.Add(new TextBlock { Text = "游戏启动参数" });
            TextBox gameArgs = new TextBox();
            gameArgs.Text = AppDataController.GetGameParameter();
            advancedPanel.Children.Add(gameArgs);
            DialogManager.RaiseDialog(XamlRoot, "高级设置", advancedPanel, true, "保存", () => AppDataController.SetGameParameter(gameArgs.Text));
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
            if (status == 0) 
            {
                selectGame.IsEnabled = true;
                selectGame.Visibility = Visibility.Visible;
                rmGame.Visibility = Visibility.Collapsed;
                advancedSettings.Visibility = Visibility.Collapsed;
                rmGame.IsEnabled = false;
                startGame.IsEnabled = false;
                startLauncher.IsEnabled = false;
                SGFrame.Visibility = Visibility.Collapsed;
            }
            else
            {
                selectGame.IsEnabled = false;
                selectGame.Visibility = Visibility.Collapsed;
                rmGame.Visibility = Visibility.Visible;
                advancedSettings.Visibility = Visibility.Visible;
                rmGame.IsEnabled = true;
                startGame.IsEnabled = true;
                startLauncher.IsEnabled = true;
                SGFrame.Visibility = Visibility.Visible;
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
            if (Process.GetProcessesByName("Wuthering Waves").Length > 0)
            {
                // 进程正在运行
                startGame.Visibility = Visibility.Collapsed;
                gameRunning.Visibility = Visibility.Visible;
                Frame_GraphicSettingView_Launched_Disable.Visibility = Visibility.Visible;
                Frame_GraphicSettingView_Launched_Disable_Title.Text = "鸣潮正在运行";
                Frame_GraphicSettingView_Launched_Disable_Subtitle.Text = "游戏运行时无法修改画质";
                Frame_AccountView_Launched_Disable.Visibility = Visibility.Visible;
                Frame_AccountView_Launched_Disable_Title.Text = "鸣潮正在运行";
                Frame_AccountView_Launched_Disable_Subtitle.Text = "游戏运行时无法切换账号";
            }
            else
            {
                // 进程未运行
                startGame.Visibility = Visibility.Visible;
                gameRunning.Visibility = Visibility.Collapsed;
                Frame_GraphicSettingView_Launched_Disable.Visibility = Visibility.Collapsed;
                Frame_AccountView_Launched_Disable.Visibility = Visibility.Collapsed;
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

        private async void CheckProcess_Graphics()
        {
            Frame_GraphicSettingView_Loading.Visibility = Visibility.Visible;
            Frame_GraphicSettingView.Content = null;
            
            if (IsWaveToolsHelperRequireUpdate)
            {
                Frame_GraphicSettingView_Disable.Visibility = Visibility.Visible;
                Frame_GraphicSettingView_Disable_Title.Text = "WaveToolsHelper需要更新";
                Frame_GraphicSettingView_Disable_Subtitle.Text = "请更新后再使用";
            }
            else
            {
                try
                {
                    string GSValue = await ProcessRun.WaveToolsHelperAsync($"/GetGS {AppDataController.GetGamePathForHelper()}");
                    if (!GSValue.Contains("QualityLevel"))
                    {
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
                    Logging.Write($"Exception in CheckProcess_Graphics: {ex.Message}", 3, "CheckProcess_Graphics");
                }
            }
        }

        private void CheckProcess_Account()
        {
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
