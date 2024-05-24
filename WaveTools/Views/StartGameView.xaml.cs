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
using Windows.Storage.Pickers;
using System.Diagnostics;
using Microsoft.UI.Dispatching;
using WaveTools.Depend;
using Spectre.Console;
using Microsoft.Win32;
using WaveTools.Views.SGViews;
using System.Net.Http;
using System.Threading.Tasks;
using static WaveTools.App;
using Windows.Foundation;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using Microsoft.UI.Xaml.Navigation;
using Windows.Devices.Geolocation;

namespace WaveTools.Views
{
    public sealed partial class StartGameView : Page
    {
        private DispatcherQueue dispatcherQueue;
/*        private DispatcherQueueTimer dispatcherTimer_Launcher;*/
        private DispatcherQueueTimer dispatcherTimer_Game;
        private DispatcherQueueTimer dispatcherTimer_Launcher;

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
            await LoadDataAsync();
            // 异步调用 GetPromptAsync
            await GetPromptAsync();
        }

        private async Task LoadDataAsync()
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
                    await CheckProcess_Graphics();
                }
            }
            else
            {
                UpdateUIElementsVisibility(0);
            }
        }

        private async void SelectGame(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".exe");
            var window = new Window();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            var file = await picker.PickSingleFileAsync();
            await AnsiConsole.Status().StartAsync("等待选择文件...", async ctx =>
            {
                if (file != null && file.Name == "Wuthering Waves.exe")
                {
                    //更新为新的存储管理机制
                    AppDataController.SetGamePath(@file.Path);
                    UpdateUIElementsVisibility(1);
                    await CheckProcess_Graphics();
                }
                else
                {
                    ValidGameFile.Subtitle = "选择正确的Wuthering Waves.exe\n通常位于[游戏根目录\\Wuthering Waves Game\\Wuthering Waves.exe]";
                    ValidGameFile.IsOpen = true;
                }
            });
        }

        public void RMGameLocation(object sender, RoutedEventArgs e)
        {
            AppDataController.RMGamePath();
            UpdateUIElementsVisibility(0);
        }
        private void ReloadFrame(object sender, RoutedEventArgs e)
        {
            UpdateUIElementsVisibility(0);
            StartGameView_Loaded(sender,e);
        }

        //启动游戏
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
                rmGame.IsEnabled = true;
                startGame.IsEnabled = true;
                startLauncher.IsEnabled = true;
                SGFrame.Visibility = Visibility.Visible;
            }
        }

        public void StartGame(TeachingTip sender, object args)
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
                Frame_GraphicSettingView_Launched_Disable.Visibility = Visibility.Visible;
                Frame_GraphicSettingView_Launched_Disable_Title.Text = "鸣潮正在运行";
                Frame_GraphicSettingView_Launched_Disable_Subtitle.Text = "游戏运行时无法修改画质";
            }
            else
            {
                // 进程未运行
                startGame.Visibility = Visibility.Visible;
                gameRunning.Visibility = Visibility.Collapsed;
                Frame_GraphicSettingView_Launched_Disable.Visibility = Visibility.Collapsed;
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

        private async Task CheckProcess_Graphics()
        {
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
                    if (!GSValue.Contains("KeyQualityLevel"))
                    {
                        GraphicSelect.IsEnabled = false;
                        GraphicSelect.IsSelected = false;
                        Frame_GraphicSettingView_Loading.Visibility = Visibility.Collapsed;
                        Frame_GraphicSettingView_Disable.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        GraphicSelect.IsEnabled = true;
                        GraphicSelect.IsSelected = true;
                        Frame_GraphicSettingView_Loading.Visibility = Visibility.Collapsed;
                        Frame_GraphicSettingView.Visibility = Visibility.Visible;
                        Frame_GraphicSettingView.Navigate(typeof(GraphicSettingView));
                    }
                }
                catch (Exception ex)
                {
                    // 处理异常，记录日志或者显示错误信息
                    Logging.Write($"Exception in CheckProcess_Graphics: {ex.Message}", 3, "CheckProcess_Graphics");
                }
            }

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
                // 处理异常，例如记录错误或显示错误消息
                Logging.Write($"Error fetching prompt: {ex.Message}", 2);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            dispatcherTimer_Game.Stop();
            dispatcherTimer_Launcher.Stop();
            Logging.Write("Timer Stopped", 0);
        }

    }
}
