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
using WaveTools.Depend;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using WaveTools.Views;
using WaveTools.Depend;


namespace WaveTools
{
    public partial class App : Application
    {
        public static MainWindow MainWindow { get; private set; }
        public static ApplicationTheme CurrentTheme { get; private set; }
        public static bool SDebugMode { get; set; }
        // 导入 AllocConsole 和 FreeConsole 函数
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllocConsole();
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();
        // 导入 GetAsyncKeyState 函数
        [DllImport("User32.dll")]
        public static extern short GetAsyncKeyState(int vKey);
        GetNotify getNotify = new GetNotify();
        private Window m_window;

        // 私有构造函数以确保单例
        public App()
        {
            InitializeComponent();
            Init();
            InitAppData();
            SetupTheme();
            InitAdminMode();
        }

        public static bool IsRequireReboot { get; set; } = false;
        public static bool IsWaveToolsRequireUpdate { get; set; } = false;
        public static bool IsWaveToolsHelperRequireUpdate { get; set; } = false;


        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            if (AppDataController.GetTerminalMode() == -1 || AppDataController.GetTerminalMode() == 0)
            {
                m_window = new MainWindow();
                m_window.Activate();
                // 处理窗口的 Closed 事件
                m_window.Closed += OnWindowClosed;
            }
            else await InitTerminalModeAsync(AppDataController.GetTerminalMode());
        }

        private void InitAppData() 
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values["Config_FirstRun"] == null)
            {
                AppDataController appDataController = new AppDataController();
                appDataController.FirstRunInit();
            }
        }

        private void InitAdminMode()
        {
            if (AppDataController.GetAdminMode() == 1)
            {
                if (!ProcessRun.IsRunAsAdmin()) ProcessRun.RequestAdminAndRestart();
            }
        }

        private void SetupTheme()
        {
            var dayNight = AppDataController.GetDayNight();
            try
            {
                if (dayNight == 1)
                {
                    this.RequestedTheme = ApplicationTheme.Light;
                }
                else if (dayNight == 2)
                {
                    this.RequestedTheme = ApplicationTheme.Dark;
                }
            }
            catch (Exception ex) 
            {
                Logging.Write(ex.StackTrace);
                NotificationManager.RaiseNotification("主题切换失败", ex.Message, InfoBarSeverity.Error);
            }
            
        }

        public async Task InitTerminalModeAsync(int Mode) 
        {
            TerminalMode.ShowConsole();
            TerminalMode terminalMode = new TerminalMode();
            bool response = await terminalMode.Init(Mode);
            if (response)
            {
                m_window = new MainWindow();
                m_window.Activate();
            }
        }

        public void Init()
        {
            AllocConsole();
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.SetWindowSize(60, 25);
            Console.SetBufferSize(60, 25);
            TerminalMode.HideConsole();
            bool isDebug = false;
            #if DEBUG
            isDebug = true;
            #else
            #endif

            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values["Config_FirstRun"] != null) { 
                switch (AppDataController.GetConsoleMode())
                {
                    case 0:
                        TerminalMode.HideConsole();
                        break;
                    case 1:
                        TerminalMode.ShowConsole();
                        break;
                    default:
                        TerminalMode.HideConsole();
                        break;
                }
            }

            if (isDebug)
            {
                Logging.Write("Debug Mode", 1);
                Console.Title = "𝐃𝐞𝐛𝐮𝐠𝐌𝐨𝐝𝐞:WaveTools";
                TerminalMode.ShowConsole();
            }
            else
            {
                Logging.Write("Release Mode", 1);
                Console.Title = "𝐍𝐨𝐫𝐦𝐚𝐥𝐌𝐨𝐝𝐞:WaveTools";
            }

            if (AppDataController.GetTerminalMode() != -1)
            {
                int Mode = (int)localSettings.Values["Config_TerminalMode"];
                TerminalMode terminalMode = new TerminalMode();
            }

        }

        private void OnWindowClosed(object sender, WindowEventArgs e)
        {
            // 关闭应用程序
            Windows.ApplicationModel.Core.CoreApplication.Exit();
        }

        public static class NotificationManager
        {
            public delegate void NotificationEventHandler(string title, string message, InfoBarSeverity severity, bool isClosable = true, int TimerSec = 0, Action action = null, string actionButtonText = null);
            public static event NotificationEventHandler OnNotificationRequested;

            public static void RaiseNotification(string title, string message, InfoBarSeverity severity, bool isClosable = true, int TimerSec = 0, Action action = null, string actionButtonText = null)
            {
                OnNotificationRequested?.Invoke(title, message, severity, isClosable, TimerSec, action, actionButtonText);
            }
        }

        public static class WaitOverlayManager
        {
            public delegate void WaitOverlayEventHandler(bool status, string title = null, string subtitle = null, bool isProgress = false, int progress = 0, bool isBtnEnabled = false, string btnContent = "", Action btnAction = null);
            public static event WaitOverlayEventHandler OnWaitOverlayRequested;

            public static void RaiseWaitOverlay(bool status, string title = null, string subtitle = null, bool isProgress = false, int progress = 0, bool isBtnEnabled = false, string btnContent = "", Action btnAction = null)
            {
                OnWaitOverlayRequested?.Invoke(status, title, subtitle, isProgress, progress, isBtnEnabled, btnContent, btnAction);
            }
        }

        public static class DialogManager
        {
            public delegate void DialogEventHandler(XamlRoot xamlRoot, string title = null, string content = null, bool isPrimaryButtonEnabled = false, string primaryButtonContent = "", Action primaryButtonAction = null, bool isSecondaryButtonEnabled = false, string secondaryButtonContent = "", Action secondaryButtonAction = null);
            public static event DialogEventHandler OnDialogRequested;

            public static void RaiseDialog(XamlRoot xamlRoot, string title = null, string content = null, bool isPrimaryButtonEnabled = false, string primaryButtonContent = "", Action primaryButtonAction = null, bool isSecondaryButtonEnabled = false, string secondaryButtonContent = "", Action secondaryButtonAction = null)
            {
                OnDialogRequested?.Invoke(xamlRoot, title, content, isPrimaryButtonEnabled, primaryButtonContent, primaryButtonAction, isSecondaryButtonEnabled, secondaryButtonContent, secondaryButtonAction);
            }
        }



    }
}
