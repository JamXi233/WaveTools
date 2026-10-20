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
using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using WaveTools.Depend;
using Windows.Graphics;
using WinRT.Interop;

namespace WaveTools
{
    public partial class App : Application
    {
        public static MainWindow MainWindow { get; private set; }
        public static ApplicationTheme CurrentTheme { get; private set; }
        public static bool GDebugMode { get; set; }
        public static bool SDebugMode { get; set; }
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllocConsole();
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();
        [DllImport("User32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        private Window m_window;

        public static bool IsRequireReboot { get; set; } = false;
        public static bool IsWaveToolsRequireUpdate { get; set; } = false;
        public static bool IsWaveToolsHelperRequireUpdate { get; set; } = false;

        public App()
        {
            InitializeComponent();
            InitAppData();
            AppDataController.SetCurrentAppVersionAsLastRunIfFirstRunCompleted();
            Init();
            SetupTheme();
            InitAdminMode();
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            int terminalMode = AppDataController.GetTerminalMode();

            if (terminalMode <= 0)
            {
                CreateMainWindow();
                return;
            }

            await InitTerminalModeAsync(terminalMode);
        }

        private void InitAppData()
        {
        }

        private void InitAdminMode()
        {
            if (AppDataController.GetAdminMode() == 1)
            {
                if (!ProcessRun.IsRunAsAdmin())
                {
                    ProcessRun.RequestAdminAndRestart();
                }
            }
        }

        private void SetupTheme()
        {
            int dayNight = AppDataController.GetDayNight();

            try
            {
                if (dayNight == 1)
                {
                    RequestedTheme = ApplicationTheme.Light;
                    CurrentTheme = ApplicationTheme.Light;
                }
                else if (dayNight == 2)
                {
                    RequestedTheme = ApplicationTheme.Dark;
                    CurrentTheme = ApplicationTheme.Dark;
                }
            }
            catch (Exception ex)
            {
                Logging.Write(ex.StackTrace);
                NotificationManager.RaiseNotification("主题切换失败", ex.Message, InfoBarSeverity.Error);
            }
        }

        public static void SetCurrentTheme(ApplicationTheme theme)
        {
            CurrentTheme = theme;
        }

        public async Task InitTerminalModeAsync(int mode)
        {
            TerminalMode.ShowConsole();

            TerminalMode terminalMode = new TerminalMode();
            bool response = await terminalMode.Init(mode);

            if (response)
            {
                CreateMainWindow();
            }
        }

        public void Init()
        {
            AllocConsole();

            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            TerminalMode.HideConsole();

            GDebugMode = false;

#if DEBUG
            GDebugMode = true;
#endif

            if (AppDataController.GetFirstRun() != -1)
            {
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
        }

        private void CreateMainWindow()
        {
            m_window = new MainWindow();
            MainWindow = (MainWindow)m_window;
            CenterWindow(m_window);
            m_window.Activate();
            m_window.Closed += OnWindowClosed;
        }

        private void OnWindowClosed(object sender, WindowEventArgs e)
        {
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
            public delegate void DialogEventHandler(XamlRoot xamlRoot, string title = null, object content = null, bool isPrimaryButtonEnabled = false, string primaryButtonContent = "", Action primaryButtonAction = null, bool isSecondaryButtonEnabled = false, string secondaryButtonContent = "", Action secondaryButtonAction = null);
            public static event DialogEventHandler OnDialogRequested;

            public static void RaiseDialog(XamlRoot xamlRoot, string title = null, object content = null, bool isPrimaryButtonEnabled = false, string primaryButtonContent = "", Action primaryButtonAction = null, bool isSecondaryButtonEnabled = false, string secondaryButtonContent = "", Action secondaryButtonAction = null)
            {
                if (content is string textContent)
                {
                    content = new TextBlock { Text = textContent };
                }

                OnDialogRequested?.Invoke(xamlRoot, title, content, isPrimaryButtonEnabled, primaryButtonContent, primaryButtonAction, isSecondaryButtonEnabled, secondaryButtonContent, secondaryButtonAction);
            }
        }

        private void CenterWindow(Window window)
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(window);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            DisplayArea displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            RectInt32 workArea = displayArea.WorkArea;

            SizeInt32 windowSize = appWindow.Size;

            int centeredX = workArea.X + (workArea.Width - windowSize.Width) / 2;
            int centeredY = workArea.Y + (workArea.Height - windowSize.Height) / 2;

            appWindow.Move(new PointInt32(centeredX, centeredY));
        }
    }
}