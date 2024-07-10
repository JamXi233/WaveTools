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
using Spectre.Console;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.Storage;
using System.IO;
using WaveTools.Depend;

namespace WaveTools.Depend
{
    internal class TerminalMode
    {

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GWL_STYLE = -16;
        private const int WS_VISIBLE = 0x10000000;

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        private Window m_window;
        private ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        public static void ShowConsole()
        {
            IntPtr handle = GetConsoleWindow();
            if (handle != IntPtr.Zero)
            {
                ShowWindow(handle, SW_SHOW);
            }
        }

        public static void HideConsole()
        {
            IntPtr handle = GetConsoleWindow();
            if (handle != IntPtr.Zero)
            {
                ShowWindow(handle, SW_HIDE);
            }
        }

        public static bool ConsoleStatus()
        {
            IntPtr handle = GetConsoleWindow();
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            int style = GetWindowLong(handle, GWL_STYLE);
            return (style & WS_VISIBLE) != 0;
        }

        public async Task<bool> Init(int Mode = 0, int SafeMode = 0, String PanicMessage = "Null",String OtherMessage = null)
        {
            var currentProcess = Process.GetCurrentProcess();
            var hWnd = currentProcess.MainWindowHandle;
            Console.Title = "WaveTools 𝑻𝒆𝒓𝒎𝒊𝒏𝒂𝒍";
            Console.Clear();
            if (SafeMode == 0) { 
                if (Mode == 1)
                {
                    var list = new[] { "选择游戏路径", "抽卡分析", "设置", "[red]退出Terminal模式[/]", "[bold red]退出WaveTools[/]", };
                    if (AppDataController.GetGamePath != null)
                    {
                        var value = localSettings.Values["Config_GamePath"] as string;
                        if (!string.IsNullOrEmpty(value) && value.Contains("Null"))
                        {
                            list = new[] { "选择游戏路径", "[Cyan]显示主界面[/]", "[red]退出Terminal模式[/]", "[bold red]退出WaveTools[/]", };
                        }
                        else
                        {
                            list = new[] { "[bold green]开启游戏[/]", "[bold yellow]清除游戏路径[/]", "[Cyan]显示主界面[/]", "[red]退出Terminal模式[/]", "[bold red]退出WaveTools[/]" };
                        }
                    }
                    else
                    {
                        list = new[] { "选择游戏路径", "[Cyan]显示主界面[/]", "[red]退出Terminal模式[/]", "[bold red]退出WaveTools[/]", };
                    }
                    var select = AnsiConsole.Prompt(
                            new SelectionPrompt<string>()
                                .Title("[bold green]WaveTools[/] Terminal模式")
                                .PageSize(10)
                                .AddChoices(list));
                    GameStartUtil gameStartUtil = new GameStartUtil();
                    switch (select)
                    {
                        case "[bold green]开启游戏[/]":
                            gameStartUtil.StartGame();
                            await Init(1);
                            return false;
                        case "[bold yellow]清除游戏路径[/]":
                            AppDataController.RMGamePath();
                            await Init(1);
                            return false;
                        case "[bold red]退出WaveTools[/]":
                            Application.Current.Exit();
                            return false;
                        case "选择游戏路径":
                            SelectGame();
                            return false;
                        case "[red]退出Terminal模式[/]":
                            localSettings.Values["Config_TerminalMode"] = 0;
                            m_window = new MainWindow();
                            m_window.Activate();
                            return false;
                        case "[Cyan]显示主界面[/]":
                            Console.Clear();
                            m_window = new MainWindow();
                            m_window.Activate();
                            return false;
                        default:
                            return false;
                    }
                }
            }
            else 
            {
                Console.Title = "WaveTools SafeMode";
                Console.Clear();
                Logging.Write("[red]错误问题：[/]" + PanicMessage,2);
                Logging.Write("其他信息：" + OtherMessage,2);
                var list = new[] { "[red]清空所有配置文件[/]", "[bold red]退出WaveTools[/]" };
                var select = AnsiConsole.Prompt(
                        new SelectionPrompt<string>()
                            .Title("\n[bold red]WaveTools 安全模式[/]")
                            .PageSize(10)
                            .AddChoices(list));
                switch (select)
                {
                    case "[red]清空所有配置文件[/]":
                        Clear_AllData(null,null);
                        return false;
                    case "[bold red]退出WaveTools[/]":
                        Application.Current.Exit();
                        return false;
                    default:
                        return false;
                }
            }
            return true;
        }

        private async void SelectGame()
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".exe");
            var window = new Window();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            var fileselect = 2;
            Console.Clear();
            Logging.Write("选择游戏文件", 0);
            Logging.Write("通常位于(游戏根目录\\Wuthering Waves Game\\Wuthering Waves.exe)", 0);
            await AnsiConsole.Status().StartAsync("等待选择文件...", async ctx =>
            {
                var file = await picker.PickSingleFileAsync();
                if (file == null) { fileselect = 1; }
                else if (file.Name == "Wuthering Waves.exe")
                {
                    localSettings.Values["Config_GamePath"] = @file.Path;
                    fileselect = 0;
                }
            });
            if (fileselect == 0)
            { await Init(1); }
            else if (fileselect == 1)
            { await Init(1); }
            else
            {
                Logging.Write("选择文件不正确，请确保是Wuthering Waves.exe\n等待3秒后重新选择", 2);
                await Task.Delay(TimeSpan.FromSeconds(3));
                SelectGame();
            }
        }

        public void Clear_AllData(object sender, RoutedEventArgs e)
        {
            string userDocumentsFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            DeleteFolder(userDocumentsFolderPath + "\\JSG-LLC\\WaveTools\\", "1");        }

        private void Clear_AllData_NoClose(object sender, RoutedEventArgs e, string Close = "0")
        {
            string userDocumentsFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            DeleteFolder(userDocumentsFolderPath + "\\JSG-LLC\\WaveTools\\", Close);
        }

        private void DeleteFolder(string folderPath, String Close)
        {
            if (Directory.Exists(folderPath))
            {
                try { Directory.Delete(folderPath, true); }
                catch (IOException) { }
            }
            _ = ClearLocalDataAsync(Close);
        }

        public async Task ClearLocalDataAsync(String Close)
        {
            // 获取 LocalData 文件夹的引用
            var localFolder = ApplicationData.Current.LocalFolder;

            // 删除 LocalData 文件夹中的所有子文件夹和文件
            await DeleteFilesAndSubfoldersAsync(localFolder, Close);

            // 需要重新创建删除的 LocalData 文件夹
            await ApplicationData.Current.ClearAsync(ApplicationDataLocality.Local);
        }

        private async Task DeleteFilesAndSubfoldersAsync(StorageFolder folder, String Close)
        {
            // 获取文件夹中的所有文件和子文件夹
            var items = await folder.GetItemsAsync();

            // 遍历所有项目
            foreach (var item in items)
            {
                // 如果项目是文件，则删除它
                if (item is StorageFile file)
                {
                    await file.DeleteAsync();
                }
                // 如果项目是文件夹，则递归删除其中所有文件和子文件夹
                else if (item is StorageFolder subfolder)
                {
                    await DeleteFilesAndSubfoldersAsync(subfolder, Close);

                    // 删除子文件夹本身
                    await subfolder.DeleteAsync();
                }
            }
            if (Close == "1")
            {
                Application.Current.Exit();
            }
        }
    }
}