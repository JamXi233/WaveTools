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
using Microsoft.UI.Xaml.Media;
using WaveTools.Depend;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using System.IO.Compression;
using Windows.Storage.Pickers;
using Newtonsoft.Json;
using System.Net.Http;
using static WaveTools.App;
using Windows.UI.Core;

namespace WaveTools.Views
{
    public sealed partial class AboutView : Page
    {
        private readonly GetJSGLatest _getJSGLatest = new GetJSGLatest();

        private bool isProgrammaticChange = false;

        [DllImport("User32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        public AboutView()
        {
            InitializeComponent();
            Logging.Write("Switch to AboutView", 0);

            this.Loaded += AboutView_Loaded;
        }

        private async void AboutView_Loaded(object sender, RoutedEventArgs e)
        {
            isProgrammaticChange = true;
            bool isDebug = Debugger.IsAttached || App.SDebugMode;
            consoleToggle.IsEnabled = !isDebug;
            debug_Mode.Visibility = isDebug ? Visibility.Visible : Visibility.Collapsed;
            debug_Message.Text = App.SDebugMode ? "您现在处于手动Debug模式" : "";
            appVersion.Text = $"WaveTools {Package.Current.Id.Version.Major}.{Package.Current.Id.Version.Minor}.{Package.Current.Id.Version.Build}.{Package.Current.Id.Version.Revision}";
            GetVersionButton();
            CheckFont();
            LoadSettings();
            await Task.Delay(200);
            isProgrammaticChange = false;
        }

        public void LoadSettings()
        {
            consoleToggle.IsChecked = AppDataController.GetConsoleMode() == 1;
            terminalToggle.IsChecked = AppDataController.GetTerminalMode() == 1;
            userviceRadio.SelectedIndex = new[] { 1, 2, 0 }[AppDataController.GetUpdateService()];
            themeRadio.SelectedIndex = AppDataController.GetDayNight();
        }


        private void CheckFont()
        {
            var fontsFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            installSFF.IsEnabled = !File.Exists(Path.Combine(fontsFolderPath, "SegoeIcons.ttf")) || !File.Exists(Path.Combine(fontsFolderPath, "Segoe Fluent Icons.ttf"));
            installSFF.Content = installSFF.IsEnabled ? "安装图标字体" : "图标字体正常";
        }


        private async void GetVersionButton()
        {
            var response = await new HttpClient().GetAsync("https://api.jamsg.cn/version");
            if (response.IsSuccessStatusCode)
            {
                var data = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
                apiVersion.Text = "ArrowAPI " + data.arrow_ver;
                antiCatVersion.Text = "AntiCat " + data.anticat_ver;
            }
        }


        private void Console_Toggle(object sender, RoutedEventArgs e)
        {
            if (consoleToggle.IsChecked ?? false) TerminalMode.ShowConsole(); else TerminalMode.HideConsole();
            AppDataController.SetConsoleMode(consoleToggle.IsChecked == true ? 1 : 0);
        }


        private void TerminalMode_Toggle(object sender, RoutedEventArgs e)
        {
            TerminalTip.IsOpen = terminalToggle.IsChecked ?? false;
            AppDataController.SetTerminalMode(terminalToggle.IsChecked == true ? 1 : 0);
        }


        public void Clear_AllData_TipShow(object sender, RoutedEventArgs e)
        {
            ClearAllDataTip.IsOpen = true;
        }

        public async void ClearAllData(TeachingTip sender, object args)
        {
            string userDocumentsFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string targetFolderPath = Path.Combine(userDocumentsFolderPath, "JSG-LLC", "WaveTools");
            await DeleteFolderAsync(targetFolderPath, true);
        }

        public async void ClearAllData_NoClose(object sender, RoutedEventArgs e, bool close = false)
        {
            string userDocumentsFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string targetFolderPath = Path.Combine(userDocumentsFolderPath, "JSG-LLC", "WaveTools");
            await DeleteFolderAsync(targetFolderPath, close);
        }

        private async Task DeleteFolderAsync(string folderPath, bool close)
        {
            if (Directory.Exists(folderPath))
            {
                try
                {
                    Directory.Delete(folderPath, true);
                }
                catch (IOException ex)
                {
                    // 可以记录日志或显示错误消息
                    Debug.WriteLine($"删除文件夹失败: {ex.Message}");
                }
            }

            await ClearLocalDataAsync(close);
        }

        public async Task ClearLocalDataAsync(bool close)
        {
            var localFolder = ApplicationData.Current.LocalFolder;
            await DeleteFilesAndSubfoldersAsync(localFolder);

            // 清除应用的本地数据
            await ApplicationData.Current.ClearAsync(ApplicationDataLocality.Local);

            // 如果需要，退出应用程序
            if (close)
            {
                Application.Current.Exit();
            }
        }

        private async Task DeleteFilesAndSubfoldersAsync(StorageFolder folder)
        {
            var items = await folder.GetItemsAsync();
            foreach (var item in items)
            {
                if (item is StorageFile file)
                {
                    await file.DeleteAsync();
                }
                else if (item is StorageFolder subfolder)
                {
                    await DeleteFilesAndSubfoldersAsync(subfolder);
                    await subfolder.DeleteAsync();
                }
            }
        }


        private async void Check_Update(object sender, RoutedEventArgs e)
        {
            UpdateTip.IsOpen = false;
            var result = await GetUpdate.GetWaveToolsUpdate();
            var status = result.Status;
            UpdateTip.Target = checkUpdate;
            UpdateTip.ActionButtonClick -= DisplayUpdateInfo;

            UpdateTip.Title = status == 0 ? "无可用更新" : status == 1 ? "有可用更新" : "网络连接失败，可能是请求次数过多";
            UpdateTip.Subtitle = status == 1 ? "新版本:" + result.Version : null;
            UpdateTip.ActionButtonContent = status == 1 ? "查看详情" : null;
            UpdateTip.CloseButtonContent = "关闭";

            if (status == 1) UpdateTip.ActionButtonClick += DisplayUpdateInfo;
            UpdateTip.IsOpen = true;
        }



        private async void Check_Depend_Update(object sender, RoutedEventArgs e)
        {
            UpdateTip.IsOpen = false;
            var result = await GetUpdate.GetDependUpdate();
            var status = result.Status;
            UpdateTip.Target = checkDependUpdate;
            UpdateTip.ActionButtonClick -= StartDependForceUpdate;
            UpdateTip.ActionButtonClick -= DisplayUpdateInfo;
            bool isShiftPressed = (GetAsyncKeyState(0x10) & 0x8000) != 0;

            UpdateTip.Title = isShiftPressed ? "遇到麻烦了吗" : status == 0 ? "无可用更新" : status == 1 ? "有可用更新" : "网络连接失败，可能是请求次数过多";
            UpdateTip.Subtitle = isShiftPressed ? "尝试重装WaveToolsHelper" : status == 1 ? "新版本:" + result.Version : null;
            UpdateTip.ActionButtonContent = isShiftPressed ? "强制重装" : status == 1 ? "查看详情" : null;
            UpdateTip.CloseButtonContent = "关闭";

            if (isShiftPressed) UpdateTip.ActionButtonClick += StartDependForceUpdate;
            else if (status == 1) UpdateTip.ActionButtonClick += DisplayUpdateInfo;

            UpdateTip.IsOpen = true;
        }

        public async void DisplayUpdateInfo(TeachingTip sender, object args)
        {
            bool isWaveTools = UpdateTip.Target != checkDependUpdate;
            UpdateResult updateinfo = isWaveTools ? await GetUpdate.GetWaveToolsUpdate() : await GetUpdate.GetDependUpdate();
            UpdateTip.IsOpen = false;
            ContentDialog updateDialog = new ContentDialog
            {
                Title = (isWaveTools ? "WaveTools" : "Helper") + ":" + updateinfo.Version + "版本可用",
                Content = "更新日志:\n" + updateinfo.Changelog,
                CloseButtonText = "关闭",
                PrimaryButtonText = "立即更新",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = sender.XamlRoot,
            };
            if (await updateDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                if (isWaveTools) StartUpdate(); else StartDependUpdate();
            }
        }

        public async void StartUpdate()
        {
            await InstallerHelper.GetInstaller();
            InstallerHelper.RunInstaller();
        }

        public async void StartDependUpdate()
        {
            WaitOverlayManager.RaiseWaitOverlay(true, true, 0, "正在更新依赖", "请稍等片刻");
            await InstallerHelper.GetInstaller();
            InstallerHelper.RunInstaller("/depend");
            WaitOverlayManager.RaiseWaitOverlay(false);
        }

        public async void StartDependForceUpdate(TeachingTip sender, object args)
        {
            UpdateTip.IsOpen = false;
            WaitOverlayManager.RaiseWaitOverlay(true, true, 0, "正在强制更新依赖", "请稍等片刻");
            await InstallerHelper.GetInstaller();
            if (InstallerHelper.RunInstaller("/depend /force") != 0)
            {
                NotificationManager.RaiseNotification("安装依赖失败", "", InfoBarSeverity.Error);
            }
            
            WaitOverlayManager.RaiseWaitOverlay(false);
        }

        // 选择主题开始
        private void ThemeRadio_Follow(object sender, RoutedEventArgs e)
        {
            if (!isProgrammaticChange) { ThemeTip.IsOpen = true; AppDataController.SetDayNight(0); }
        }

        private void ThemeRadio_Light(object sender, RoutedEventArgs e)
        {
            if (!isProgrammaticChange) { ThemeTip.IsOpen = true; AppDataController.SetDayNight(1); }
        }

        private void ThemeRadio_Dark(object sender, RoutedEventArgs e)
        {
            if (!isProgrammaticChange) { ThemeTip.IsOpen = true; AppDataController.SetDayNight(2); }
        }

        // 选择下载渠道开始
        private void uservice_Github_Choose(object sender, RoutedEventArgs e)
        {
            if (!isProgrammaticChange) { AppDataController.SetUpdateService(0); }
        }

        private void uservice_Gitee_Choose(object sender, RoutedEventArgs e)
        {
            if (!isProgrammaticChange) { AppDataController.SetUpdateService(1); }
        }

        private void uservice_JSG_Choose(object sender, RoutedEventArgs e)
        {
            if (!isProgrammaticChange) { AppDataController.SetUpdateService(2); }
        }


        private async void Backup_Data(object sender, RoutedEventArgs e)
        {
            DateTime now = DateTime.Now;
            string formattedDate = now.ToString("yyyy_MM_dd_HH_mm_ss");
            string userDocumentsFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var savePicker = new FileSavePicker();
            savePicker.FileTypeChoices.Add("Zip Archive", new List<string>() { ".WaveToolsBackup" });
            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.SuggestedFileName = "WaveTools_Backup_" + formattedDate;
            var window = new Window();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                string startPath = userDocumentsFolderPath + @"\JSG-LLC\WaveTools";
                string zipPath = file.Path;
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
                ZipFile.CreateFromDirectory(startPath, zipPath);
            }
        }

        private void Restore_Data_Click(object sender, RoutedEventArgs e)
        {
            RestoreTip.IsOpen = true;
        }

        private async void Restore_Data(TeachingTip sender, object args)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".WaveToolsBackup");

            var window = new Window();
            try
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    string userDocumentsFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    try
                    {
                        // 使用异步方式执行数据清理和解压缩操作
                        await Task.Run(() => ClearAllData_NoClose(null, null));
                        await Task.Run(() => ZipFile.ExtractToDirectory(file.Path, userDocumentsFolderPath + "\\JSG-LLC\\WaveTools\\"));
                    }
                    catch (Exception ex)
                    {
                        // 添加日志或错误处理逻辑
                        Debug.WriteLine("Error during restore process: " + ex.Message);
                        sender.Subtitle = "Restore failed: " + ex.Message;
                        sender.IsOpen = true;
                        return;
                    }

                    // 重启应用
                    await ProcessRun.RestartApp();
                }
            }
            finally
            {
                window.Close();  // 确保Window在结束时关闭，释放资源
            }
        }

        private async void Install_Font_Click(object sender, RoutedEventArgs e)
        {
            installSFF.IsEnabled = false;
            installSFF_Progress.Visibility = Visibility.Visible;
            var progress = new Progress<double>();

            await InstallFont.InstallSegoeFluentFontAsync(progress);
            installSFF.Content = "安装字体后重启WaveTools即生效";
            installSFF_Progress.Visibility = Visibility.Collapsed;
        }

        private async void Restart_App(TeachingTip sender, object args)
        {
            await ProcessRun.RestartApp();
        }

        // Debug_Clicks
        private void Debug_Panic_Click(object sender, RoutedEventArgs e) 
        {
            throw new Exception("全局异常处理测试");
        }

        private void Debug_Notification_Test(object sender, RoutedEventArgs e)
        {
            NotificationManager.RaiseNotification("测试通知","这是一条测试通知", InfoBarSeverity.Success);
        }

        // Debug_Disable_NavBtns
        private void Debug_Disable_NavBtns(object sender, RoutedEventArgs e)
        {
            NavigationView parentNavigationView = GetParentNavigationView(this);
            if (debug_DisableNavBtns.IsChecked == true)
            {
                if (parentNavigationView != null)
                {
                    foreach (var menuItem in parentNavigationView.MenuItems)
                    {
                        if (menuItem is NavigationViewItem navViewItem)
                        {
                            navViewItem.IsEnabled = false;
                        }
                    }
                    foreach (var menuItem in parentNavigationView.FooterMenuItems)
                    {
                        if (menuItem is NavigationViewItem navViewItem)
                        {
                            navViewItem.IsEnabled = false;
                        }
                    }
                }
            }
            else
            {
                if (parentNavigationView != null)
                {
                    foreach (var menuItem in parentNavigationView.MenuItems)
                    {
                        if (menuItem is NavigationViewItem navViewItem)
                        {
                            navViewItem.IsEnabled = true;
                        }
                    }
                    foreach (var menuItem in parentNavigationView.FooterMenuItems)
                    {
                        if (menuItem is NavigationViewItem navViewItem)
                        {
                            navViewItem.IsEnabled = true;
                        }
                    }
                }
            }
        }

        private NavigationView GetParentNavigationView(FrameworkElement child)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);

            while (parent != null && !(parent is NavigationView))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            return parent as NavigationView;
        }

    }
}