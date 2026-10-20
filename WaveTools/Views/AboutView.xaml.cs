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
using Microsoft.UI.Xaml.Media.Animation;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WaveTools.Depend;
using Windows.Foundation;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using static WaveTools.App;
using static WaveTools.Depend.CommonHelpers;

namespace WaveTools.Views
{
    public sealed partial class AboutView : Page
    {
        private readonly GetGithubLatest _getGithubLatest = new GetGithubLatest();
        private readonly GetJSGLatest _getJSGLatest = new GetJSGLatest();

        private bool isProgrammaticChange = false;
        private bool isStorageManagementExpanded = false;
        private Storyboard storageManagementStoryboard;
        private static readonly SemaphoreSlim StorageOperationSemaphore = new SemaphoreSlim(1, 1);

        [DllImport("User32.dll")]
        public static extern short GetAsyncKeyState(int vKey);
        private static int Notification_Test_Count = 0;

        public AboutView()
        {
            InitializeComponent();
            Logging.Write("Switch to AboutView", 0);

            this.Loaded += AboutView_Loaded;
        }

        private async void AboutView_Loaded(object sender, RoutedEventArgs e)
        {
            Logging.Write("AboutView loaded", 0);
            isProgrammaticChange = true;
            bool isDebug = Debugger.IsAttached || App.SDebugMode;
            debug_Mode.Visibility = isDebug ? Visibility.Visible : Visibility.Collapsed;
            debug_Message.Text = App.SDebugMode ? "您现在处于手动Debug模式" : "";
            appVersion.Text = $"WaveTools {CommonHelpers.AppVersionHelper.GetAppVersion()}";
            Logging.Write($"App version: {appVersion.Text}", 0);
            GetVersionButton();
            CheckFont();
            LoadSettings();
            await Task.Delay(200);
            isProgrammaticChange = false;
        }

        public void LoadSettings()
        {
            Logging.Write("Loading settings", 0);
            consoleToggle.IsOn = AppDataController.GetConsoleMode() == 1;
            terminalToggle.IsOn = AppDataController.GetTerminalMode() == 1;
            autoCheckUpdateToggle.IsOn = AppDataController.GetAutoCheckUpdate() == 1;
            adminModeToggle.IsOn = AppDataController.GetAdminMode() == 1;

            int updateService = AppDataController.GetUpdateService();
            userviceCombo.SelectedIndex = updateService == 0 ? 1 : updateService == 2 ? 0 : -1;
            themeCombo.SelectedIndex = AppDataController.GetDayNight();

            if (dataRootPathText != null)
            {
                dataRootPathText.Text = AppDataController.DataRootPath;
            }

            UpdateToggleStateTexts();
            _ = RefreshStorageUsageAsync();
        }

        private void UpdateToggleStateTexts()
        {
            if (consoleToggleStateText != null)
            {
                consoleToggleStateText.Text = consoleToggle.IsOn ? "开" : "关";
            }

            if (terminalToggleStateText != null)
            {
                terminalToggleStateText.Text = terminalToggle.IsOn ? "开" : "关";
            }

            if (autoCheckUpdateToggleStateText != null)
            {
                autoCheckUpdateToggleStateText.Text = autoCheckUpdateToggle.IsOn ? "开" : "关";
            }

            if (adminModeToggleStateText != null)
            {
                adminModeToggleStateText.Text = adminModeToggle.IsOn ? "开" : "关";
            }
        }

        private void CheckFont()
        {
            Logging.Write("Checking fonts", 0);
            var fontsFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            installSFF.IsEnabled = !File.Exists(Path.Combine(fontsFolderPath, "SegoeIcons.ttf")) || !File.Exists(Path.Combine(fontsFolderPath, "Segoe Fluent Icons.ttf"));
            installSFF.Content = installSFF.IsEnabled ? "安装图标字体" : "图标字体正常";
        }

        private async void GetVersionButton()
        {
            Logging.Write("Getting version information", 0);
            try
            {
                var response = await new HttpClient().GetAsync("https://api.jamsg.cn/version");
                if (response.IsSuccessStatusCode)
                {
                    var data = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
                    apiVersion.Text = "ArrowAPI " + data.arrow_ver;
                    antiCatVersion.Text = "AntiCat " + data.anticat_ver;
                    apiVersion.Visibility = Visibility.Visible;
                    antiCatVersion.Visibility = Visibility.Visible;
                    Logging.Write($"API Version: {apiVersion.Text}, AntiCat Version: {antiCatVersion.Text}", 0);
                }
                else
                {
                    Logging.Write("Failed to get version information", 1);
                }
            }
            catch (Exception ex)
            {
                Logging.Write("Failed to get version information: " + ex.Message, 1);
            }
        }

        private void Console_Toggle(object sender, RoutedEventArgs e)
        {
            if (isProgrammaticChange)
            {
                return;
            }

            Logging.Write("Toggling console mode", 0);
            if (consoleToggle.IsOn) TerminalMode.ShowConsole(); else TerminalMode.HideConsole();
            AppDataController.SetConsoleMode(consoleToggle.IsOn ? 1 : 0);
            UpdateToggleStateTexts();
        }

        private void TerminalMode_Toggle(object sender, RoutedEventArgs e)
        {
            if (isProgrammaticChange)
            {
                return;
            }

            Logging.Write("Toggling terminal mode", 0);
            TerminalTip.IsOpen = terminalToggle.IsOn;
            AppDataController.SetTerminalMode(terminalToggle.IsOn ? 1 : 0);
            UpdateToggleStateTexts();
        }

        private void Auto_Check_Update_Toggle(object sender, RoutedEventArgs e)
        {
            if (isProgrammaticChange)
            {
                return;
            }

            AppDataController.SetAutoCheckUpdate(autoCheckUpdateToggle.IsOn ? 1 : 0);
            UpdateToggleStateTexts();
        }

        private void Admin_Mode_Toggle(object sender, RoutedEventArgs e)
        {
            if (isProgrammaticChange)
            {
                return;
            }

            AppDataController.SetAdminMode(adminModeToggle.IsOn ? 1 : 0);
            UpdateToggleStateTexts();
        }

        public void Clear_AllData_TipShow(object sender, RoutedEventArgs e)
        {
            Logging.Write("Showing Clear All Data Tip", 0);
            ClearAllDataTip.IsOpen = true;
        }

        public async void ClearAllData(TeachingTip sender, object args)
        {
            Logging.Write("Clearing all data", 0);
            string targetFolderPath = AppDataController.DataRootPath;
            await DeleteFolderAsync(targetFolderPath, true);
        }

        public async void ClearAllData_NoClose(object sender, RoutedEventArgs e, bool close = false)
        {
            Logging.Write("Clearing all data without closing", 0);
            string targetFolderPath = AppDataController.DataRootPath;
            await DeleteFolderAsync(targetFolderPath, close);
        }

        private async Task DeleteFolderAsync(string folderPath, bool close)
        {
            Logging.Write($"Deleting folder: {folderPath}", 0);
            if (Directory.Exists(folderPath))
            {
                try
                {
                    Directory.Delete(folderPath, true);
                    Logging.Write("Folder deleted successfully", 0);
                }
                catch (IOException ex)
                {
                    Logging.Write($"Failed to delete folder: {ex.Message}", 1);
                    Debug.WriteLine($"删除文件夹失败: {ex.Message}");
                }
            }

            await ClearLocalDataAsync(close);
        }

        public async Task ClearLocalDataAsync(bool close)
        {
            Logging.Write("Clearing local data", 0);
            if (AppVersionHelper.IsPackaged())
            {
                var applicationData = ApplicationData.Current;
                await DeleteFilesAndSubfoldersAsync(applicationData.LocalFolder);

                // 仅具有包身份的进程支持访问应用包的本地数据。
                await applicationData.ClearAsync(ApplicationDataLocality.Local);
            }

            AppDataController.ResetCache();

            // 如果需要，退出应用程序
            if (close)
            {
                Logging.Write("Exiting application", 0);
                Application.Current.Exit();
            }
        }

        private async Task DeleteFilesAndSubfoldersAsync(StorageFolder folder)
        {
            Logging.Write($"Deleting files and subfolders in: {folder.Path}", 0);
            var items = await folder.GetItemsAsync();
            foreach (var item in items)
            {
                if (item is StorageFile file)
                {
                    await file.DeleteAsync();
                    Logging.Write($"Deleted file: {file.Path}", 0);
                }
                else if (item is StorageFolder subfolder)
                {
                    await DeleteFilesAndSubfoldersAsync(subfolder);
                    await subfolder.DeleteAsync();
                    Logging.Write($"Deleted subfolder: {subfolder.Path}", 0);
                }
            }
        }

        private async void Check_Update(object sender, RoutedEventArgs e)
        {
            Logging.Write("Checking for updates", 0);
            UpdateTip.IsOpen = false;
            UpdateTip.Target = checkUpdate;
            UpdateTip.ActionButtonClick -= StartDependForceUpdate;
            UpdateTip.ActionButtonClick -= StartForceUpdate;
            UpdateTip.ActionButtonClick -= DisplayUpdateInfo;
            bool isShiftPressed = (GetAsyncKeyState(0x10) & 0x8000) != 0;

            if (CommonHelpers.AppVersionHelper.IsPortable())
            {
                CommonHelpers.PortableUpdateInfo portableResult = await CommonHelpers.PortableUpdateHelper.GetPortableUpdateAsync(isShiftPressed);
                int portableStatus = portableResult.Status;
                UpdateTip.Title = isShiftPressed ? "遇到麻烦了吗" : portableStatus == 0 ? "无可用更新" : portableStatus == 1 ? "有可用更新" : "网络连接失败，可能是请求次数过多";
                UpdateTip.Subtitle = isShiftPressed ? "尝试重装WaveTools" : portableStatus == 1 ? "新版本:" + portableResult.Version : null;
                UpdateTip.ActionButtonContent = isShiftPressed ? "强制重装" : portableStatus == 1 ? "查看详情" : null;
                UpdateTip.CloseButtonContent = "关闭";

                if (isShiftPressed)
                {
                    UpdateTip.ActionButtonClick += StartForceUpdate;
                }

                if (portableStatus == 1)
                {
                    UpdateTip.ActionButtonClick += DisplayUpdateInfo;
                }

                UpdateTip.IsOpen = true;
                return;
            }

            var result = await GetUpdate.GetWaveToolsUpdate();
            var status = result.Status;

            UpdateTip.Title = isShiftPressed ? "遇到麻烦了吗" : status == 0 ? "无可用更新" : status == 1 ? "有可用更新" : "网络连接失败，可能是请求次数过多";
            UpdateTip.Subtitle = isShiftPressed ? "尝试重装WaveTools" : status == 1 ? "新版本:" + result.Version : null;
            UpdateTip.ActionButtonContent = isShiftPressed ? "强制重装" : status == 1 ? "查看详情" : null;
            UpdateTip.CloseButtonContent = "关闭";

            if (isShiftPressed) UpdateTip.ActionButtonClick += StartForceUpdate;
            if (status == 1) UpdateTip.ActionButtonClick += DisplayUpdateInfo;
            UpdateTip.IsOpen = true;
        }

        private async void Check_Depend_Update(object sender, RoutedEventArgs e)
        {
            Logging.Write("Checking for dependency updates", 0);
            UpdateTip.IsOpen = false;
            UpdateResult result = await GetDependUpdateForCurrentMode();
            var status = result.Status;
            UpdateTip.Target = checkDependUpdate;
            UpdateTip.ActionButtonClick -= StartDependForceUpdate;
            UpdateTip.ActionButtonClick -= StartForceUpdate;
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

        private async Task<UpdateResult> GetDependUpdateForCurrentMode()
        {
            if (!CommonHelpers.AppVersionHelper.IsPortable())
            {
                return await GetUpdate.GetDependUpdate();
            }

            try
            {
                (string Name, string Version, string DownloadUrl, string Changelog) latestReleaseInfo;

                if (AppDataController.GetUpdateService() == 0)
                {
                    latestReleaseInfo = await _getGithubLatest.GetLatestDependReleaseInfoAsync("JamXi233", "Releases", "WaveToolsHelper");
                }
                else
                {
                    try
                    {
                        latestReleaseInfo = await _getJSGLatest.GetLatestReleaseInfoAsync("cn.jamsg.WaveToolshelper");
                    }
                    catch
                    {
                        latestReleaseInfo = await _getJSGLatest.GetLatestReleaseInfoAsync("cn.jamsg.WaveToolsHelper");
                    }
                }

                string exePath = AppDataController.GetDataPath("Depends", "WaveToolsHelper", "WaveToolsHelper.exe");
                Version installedVersion = new Version("0.0.0.0");

                if (File.Exists(exePath))
                {
                    FileVersionInfo fileInfo = FileVersionInfo.GetVersionInfo(exePath);

                    if (!string.IsNullOrWhiteSpace(fileInfo.FileVersion) && Version.TryParse(fileInfo.FileVersion, out Version parsedVersion))
                    {
                        installedVersion = parsedVersion;
                    }
                }

                if (!Version.TryParse(latestReleaseInfo.Version, out Version latestVersion))
                {
                    return new UpdateResult(2, string.Empty, string.Empty);
                }

                if (latestVersion > installedVersion)
                {
                    App.IsWaveToolsHelperRequireUpdate = true;
                    return new UpdateResult(1, latestReleaseInfo.Version, latestReleaseInfo.Changelog);
                }

                App.IsWaveToolsHelperRequireUpdate = false;
                return new UpdateResult(0, installedVersion.ToString(), string.Empty);
            }
            catch
            {
                App.IsWaveToolsHelperRequireUpdate = false;
                return new UpdateResult(2, string.Empty, string.Empty);
            }
        }

        public async void DisplayUpdateInfo(TeachingTip sender, object args)
        {
            Logging.Write("Displaying update information", 0);
            bool isWaveTools = UpdateTip.Target != checkDependUpdate;
            string title;
            string content;
            Action action;

            if (isWaveTools && CommonHelpers.AppVersionHelper.IsPortable())
            {
                CommonHelpers.PortableUpdateInfo updateInfo = await CommonHelpers.PortableUpdateHelper.GetPortableUpdateAsync();
                title = "WaveTools:" + updateInfo.Version + "版本可用";
                content = "更新日志:\n" + updateInfo.Changelog;
                action = StartUpdate;
            }
            else
            {
                UpdateResult updateInfo = isWaveTools ? await GetUpdate.GetWaveToolsUpdate() : await GetDependUpdateForCurrentMode();
                title = (isWaveTools ? "WaveTools" : "Helper") + ":" + updateInfo.Version + "版本可用";
                content = "更新日志:\n" + updateInfo.Changelog;
                action = isWaveTools ? StartUpdate : StartDependUpdate;
            }

            UpdateTip.IsOpen = false;
            string primaryButtonText = "立即更新";
            XamlRoot xamlRoot = sender.XamlRoot;

            DialogManager.RaiseDialog(xamlRoot, title, content, true, primaryButtonText, action);
        }

        public async void StartUpdate()
        {
            if (CommonHelpers.AppVersionHelper.IsPortable())
            {
                await StartPortableUpdateAsync(false);
                return;
            }

            UpdateTip.IsOpen = false;
            WaitOverlayManager.RaiseWaitOverlay(true, "正在更新", "请稍等片刻", true, 0);
            await InstallerHelper.GetInstaller();
            string channelArgument = GetChannelArgument();
            if (InstallerHelper.RunInstaller(channelArgument) != 0)
            {
                NotificationManager.RaiseNotification("更新失败", "", InfoBarSeverity.Error, true, 3);
            }
            WaitOverlayManager.RaiseWaitOverlay(false);
        }

        public async void StartForceUpdate(TeachingTip sender, object args)
        {
            if (CommonHelpers.AppVersionHelper.IsPortable())
            {
                await StartPortableUpdateAsync(true);
                return;
            }

            UpdateTip.IsOpen = false;
            WaitOverlayManager.RaiseWaitOverlay(true, "正在强制重装WaveTools", "请稍等片刻", true, 0);
            await InstallerHelper.GetInstaller();
            string channelArgument = GetChannelArgument();
            if (InstallerHelper.RunInstaller($"/force {channelArgument}") != 0)
            {
                NotificationManager.RaiseNotification("更新失败", "", InfoBarSeverity.Error, true, 3);
            }
            WaitOverlayManager.RaiseWaitOverlay(false);
        }

        private async Task StartPortableUpdateAsync(bool force)
        {
            UpdateTip.IsOpen = false;
            WaitOverlayManager.RaiseWaitOverlay(true, force ? "正在强制重装便携版" : "正在更新便携版", "正在下载便携版更新器", true, 0);

            try
            {
                string updaterPath = await CommonHelpers.PortableUpdateHelper.GetPortableUpdaterAsync(true);

                if (string.IsNullOrWhiteSpace(updaterPath) || !File.Exists(updaterPath))
                {
                    NotificationManager.RaiseNotification("启动便携版更新失败", "未找到WaveTools.Updater.exe", InfoBarSeverity.Error, true, 5);
                    return;
                }

                WaitOverlayManager.RaiseWaitOverlay(true, force ? "正在强制重装便携版" : "正在更新便携版", "正在启动便携版更新器", true, 0);

                CommonHelpers.PortableUpdateInfo updateInfo = await CommonHelpers.PortableUpdateHelper.GetLatestPortableReleaseInfoAsync();
                string arguments = CommonHelpers.PortableUpdateHelper.BuildUpdaterArguments(updateInfo, AppDataController.DataRootPath, force);
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(updaterPath),
                    UseShellExecute = true
                };

                Process.Start(startInfo);
                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                NotificationManager.RaiseNotification("启动便携版更新失败", ex.Message, InfoBarSeverity.Error, true, 5);
            }
            finally
            {
                WaitOverlayManager.RaiseWaitOverlay(false);
            }
        }

        public async void StartDependUpdate()
        {
            UpdateTip.IsOpen = false;
            WaitOverlayManager.RaiseWaitOverlay(true, "正在更新依赖", "请稍等片刻", true, 0);
            await InstallerHelper.GetInstaller();
            string channelArgument = GetChannelArgument();
            InstallerHelper.RunInstaller($"/depend {channelArgument}");
            WaitOverlayManager.RaiseWaitOverlay(false);
        }

        public async void StartDependForceUpdate(TeachingTip sender, object args)
        {
            UpdateTip.IsOpen = false;
            WaitOverlayManager.RaiseWaitOverlay(true, "正在强制重装依赖", "请稍等片刻", true, 0);
            await InstallerHelper.GetInstaller();
            string channelArgument = GetChannelArgument();
            if (InstallerHelper.RunInstaller($"/depend /force {channelArgument}") != 0)
            {
                NotificationManager.RaiseNotification("强制重装依赖失败", "", InfoBarSeverity.Error, true, 3);
            }
            WaitOverlayManager.RaiseWaitOverlay(false);
        }

        private string GetChannelArgument()
        {
            int channel = AppDataController.GetUpdateService();
            return channel switch
            {
                0 => "/channel github",
                2 => "/channel ds",
                _ => string.Empty
            };
        }

        // 选择主题开始
        private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isProgrammaticChange)
            {
                return;
            }

            int selectedIndex = themeCombo.SelectedIndex;
            if (selectedIndex < 0)
            {
                return;
            }

            Logging.Write("Selected theme index: " + selectedIndex, 0);
            AppDataController.SetDayNight(selectedIndex);
            App.MainWindow?.ApplyThemeNow(selectedIndex);
        }

        // 选择下载渠道开始
        private void UpdateServiceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isProgrammaticChange)
            {
                return;
            }

            if (userviceCombo.SelectedIndex == 0)
            {
                Logging.Write("Selected update service: JSG", 0);
                AppDataController.SetUpdateService(2);
                return;
            }

            if (userviceCombo.SelectedIndex == 1)
            {
                Logging.Write("Selected update service: Github", 0);
                AppDataController.SetUpdateService(0);
            }
        }

        private async void Change_DataRoot_Click(object sender, RoutedEventArgs e)
        {
            string folderPath = await PickFolderAsync();
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            if (AppDataController.IsCurrentDataRoot(folderPath))
            {
                NotificationManager.RaiseNotification("数据目录未改变", "选择的目录与当前数据目录相同", InfoBarSeverity.Warning, true, 2);
                return;
            }

            if (!EnsureTargetDataRootEmpty(folderPath))
            {
                return;
            }

            string currentPath = AppDataController.DataRootPath;

            StackPanel content = new StackPanel
            {
                Spacing = 8
            };

            content.Children.Add(new TextBlock
            {
                Text = "将把当前数据完整移动到新目录\n移动完成并校验通过后才会删除旧目录",
                TextWrapping = TextWrapping.Wrap
            });

            content.Children.Add(new TextBlock
            {
                Text = "当前目录：\n" + currentPath,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.75
            });

            content.Children.Add(new TextBlock
            {
                Text = "新目录：\n" + folderPath,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.75
            });

            DialogManager.RaiseDialog(
                XamlRoot,
                "移动数据保存目录",
                content,
                true,
                "开始移动",
                async () => await ApplyDataRootMoveAsync(folderPath)
            );
        }

        private void Reset_DataRoot_Click(object sender, RoutedEventArgs e)
        {
            if (AppDataController.IsCurrentDataRoot(AppDataController.DefaultDataRootPath) && !AppDataController.IsUsingCustomDataRoot())
            {
                NotificationManager.RaiseNotification("当前已在使用默认数据目录", "", InfoBarSeverity.Warning, true, 2);
                return;
            }

            string currentPath = AppDataController.DataRootPath;
            string defaultPath = AppDataController.DefaultDataRootPath;

            if (!AppDataController.IsCurrentDataRoot(defaultPath) && !EnsureTargetDataRootEmpty(defaultPath))
            {
                return;
            }

            StackPanel content = new StackPanel
            {
                Spacing = 8
            };

            content.Children.Add(new TextBlock
            {
                Text = "将把当前数据完整移动回默认目录\n移动完成并校验通过后才会删除旧目录",
                TextWrapping = TextWrapping.Wrap
            });

            content.Children.Add(new TextBlock
            {
                Text = "当前目录：\n" + currentPath,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.75
            });

            content.Children.Add(new TextBlock
            {
                Text = "默认目录：\n" + defaultPath,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.75
            });

            DialogManager.RaiseDialog(
                XamlRoot,
                "恢复默认数据目录",
                content,
                true,
                "开始移动",
                async () => await ApplyDefaultDataRootMoveAsync()
            );
        }

        private void Open_DataRoot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string dataRootPath = AppDataController.DataRootPath;
                Directory.CreateDirectory(dataRootPath);
                Process.Start("explorer.exe", "\"" + dataRootPath + "\"");
            }
            catch (Exception ex)
            {
                NotificationManager.RaiseNotification("打开数据目录失败", ex.Message, InfoBarSeverity.Error);
            }
        }

        private bool EnsureTargetDataRootEmpty(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return false;
            }

            try
            {
                if (!Directory.Exists(folderPath))
                {
                    return true;
                }

                if (!Directory.EnumerateFileSystemEntries(folderPath).Any())
                {
                    return true;
                }

                NotificationManager.RaiseNotification("目录不为空", folderPath + "目录不为空\n请选择一个空的目录来迁移数据", InfoBarSeverity.Warning, true, 5);
                return false;
            }
            catch (Exception ex)
            {
                NotificationManager.RaiseNotification("检查目录失败", ex.Message, InfoBarSeverity.Error, true, 5);
                return false;
            }
        }

        private void ShowDataRootLockedDialog(
    IReadOnlyList<LockingProcessInfo> lockingProcesses,
    IReadOnlyList<BlockedPathInfo> blockedPaths)
        {
            StackPanel content = new StackPanel
            {
                Spacing = 8
            };

            content.Children.Add(new TextBlock
            {
                Text = "检测到数据目录正在被占用\n请关闭占用程序后再重试移动",
                TextWrapping = TextWrapping.Wrap
            });

            if (lockingProcesses.Count > 0)
            {
                content.Children.Add(new TextBlock
                {
                    Text = "占用进程：",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 8, 0, 0)
                });

                foreach (LockingProcessInfo process in lockingProcesses.Take(10))
                {
                    string processText = process.DisplayName;

                    if (!string.IsNullOrWhiteSpace(process.ExecutablePath))
                    {
                        processText += "\n" + process.ExecutablePath;
                    }

                    content.Children.Add(new TextBlock
                    {
                        Text = "• " + processText,
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.82
                    });
                }

                if (lockingProcesses.Count > 10)
                {
                    content.Children.Add(new TextBlock
                    {
                        Text = $"还有 {lockingProcesses.Count - 10} 个进程未显示",
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.65
                    });
                }
            }

            if (blockedPaths.Count > 0)
            {
                content.Children.Add(new TextBlock
                {
                    Text = "被占用目录：",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 8, 0, 0)
                });

                foreach (BlockedPathInfo blockedPath in blockedPaths.Take(10))
                {
                    content.Children.Add(new TextBlock
                    {
                        Text = "• " + blockedPath.DisplayName,
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.82
                    });
                }

                if (blockedPaths.Count > 10)
                {
                    content.Children.Add(new TextBlock
                    {
                        Text = $"还有 {blockedPaths.Count - 10} 个目录未显示",
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.65
                    });
                }
            }

            DialogManager.RaiseDialog(
                XamlRoot,
                "数据目录被占用",
                content,
                false,
                "关闭",
                null
            );
        }

        private async Task<bool> CheckDataRootLockedBeforeMoveAsync(params string[] paths)
        {
            DataRootLockCheckResult checkResult = null;

            WaitOverlayManager.RaiseWaitOverlay(true, "正在检查数据目录占用", "正在检测是否有程序正在占用数据目录", true, 0);

            try
            {
                checkResult = await Task.Run(() => BuildDataRootLockCheckResult(paths));
            }
            catch (Exception ex)
            {
                Logging.Write($"Data root lock check failed: {ex.Message}", 2);
                NotificationManager.RaiseNotification("目录占用检查失败", ex.Message, InfoBarSeverity.Error, true, 5);
                return false;
            }
            finally
            {
                WaitOverlayManager.RaiseWaitOverlay(false);
            }

            if (checkResult == null || !checkResult.HasBlockedItems)
            {
                return true;
            }
            await Task.Delay(180);

            NotificationManager.RaiseNotification("数据目录被占用", "已拦截移动操作\n请查看占用详情", InfoBarSeverity.Warning, true, 4);
            await ShowDataRootLockedDialogAsync(checkResult.LockingProcesses, checkResult.BlockedPaths);

            return false;
        }

        private DataRootLockCheckResult BuildDataRootLockCheckResult(params string[] paths)
        {
            Dictionary<int, LockingProcessInfo> processResult = new Dictionary<int, LockingProcessInfo>();
            Dictionary<string, BlockedPathInfo> pathResult = new Dictionary<string, BlockedPathInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (!Directory.Exists(path) && !File.Exists(path))
                {
                    continue;
                }

                foreach (LockingProcessInfo process in DataRootLockDetector.FindLockingProcesses(path))
                {
                    processResult[process.ProcessId] = process;
                }

                foreach (BlockedPathInfo blockedPath in DataRootLockDetector.FindBlockedDirectoryPaths(path))
                {
                    pathResult[blockedPath.Path] = blockedPath;
                }
            }

            return new DataRootLockCheckResult
            {
                LockingProcesses = processResult.Values
                    .OrderBy(item => item.ProcessName)
                    .ThenBy(item => item.ProcessId)
                    .ToList(),

                BlockedPaths = pathResult.Values
                    .OrderBy(item => item.Path)
                    .ToList()
            };
        }

        private async Task ShowDataRootLockedDialogAsync(
    IReadOnlyList<LockingProcessInfo> lockingProcesses,
    IReadOnlyList<BlockedPathInfo> blockedPaths)
        {
            StackPanel contentPanel = new StackPanel
            {
                Spacing = 8
            };

            contentPanel.Children.Add(new TextBlock
            {
                Text = "检测到数据目录正在被占用\n已取消本次移动，请关闭占用程序后再重试",
                TextWrapping = TextWrapping.Wrap
            });

            if (lockingProcesses.Count > 0)
            {
                contentPanel.Children.Add(new TextBlock
                {
                    Text = "占用进程：",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 8, 0, 0)
                });

                foreach (LockingProcessInfo process in lockingProcesses.Take(12))
                {
                    string processText = process.DisplayName;

                    if (!string.IsNullOrWhiteSpace(process.ExecutablePath))
                    {
                        processText += "\n" + process.ExecutablePath;
                    }

                    contentPanel.Children.Add(new TextBlock
                    {
                        Text = "• " + processText,
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.82
                    });
                }

                if (lockingProcesses.Count > 12)
                {
                    contentPanel.Children.Add(new TextBlock
                    {
                        Text = $"还有 {lockingProcesses.Count - 12} 个进程未显示",
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.65
                    });
                }
            }

            if (blockedPaths.Count > 0)
            {
                contentPanel.Children.Add(new TextBlock
                {
                    Text = "被占用目录：",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Margin = new Thickness(0, 8, 0, 0)
                });

                foreach (BlockedPathInfo blockedPath in blockedPaths.Take(12))
                {
                    contentPanel.Children.Add(new TextBlock
                    {
                        Text = "• " + blockedPath.DisplayName,
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.82
                    });
                }

                if (blockedPaths.Count > 12)
                {
                    contentPanel.Children.Add(new TextBlock
                    {
                        Text = $"还有 {blockedPaths.Count - 12} 个目录未显示",
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.65
                    });
                }
            }

            ScrollViewer scrollViewer = new ScrollViewer
            {
                Content = contentPanel,
                MaxHeight = 420,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            ContentDialog dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "数据目录被占用",
                Content = scrollViewer,
                CloseButtonText = "关闭",
                DefaultButton = ContentDialogButton.Close
            };

            try
            {
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Logging.Write($"Show data root locked dialog failed: {ex.Message}", 2);

                string fallbackMessage = BuildDataRootLockedFallbackMessage(lockingProcesses, blockedPaths);
                NotificationManager.RaiseNotification("数据目录被占用", fallbackMessage, InfoBarSeverity.Warning, true, 8);
            }
        }

        private string BuildDataRootLockedFallbackMessage(
    IReadOnlyList<LockingProcessInfo> lockingProcesses,
    IReadOnlyList<BlockedPathInfo> blockedPaths)
        {
            List<string> lines = new List<string>();

            foreach (LockingProcessInfo process in lockingProcesses.Take(3))
            {
                lines.Add(process.DisplayName);
            }

            foreach (BlockedPathInfo blockedPath in blockedPaths.Take(3))
            {
                lines.Add(blockedPath.Path);
            }

            if (lines.Count == 0)
            {
                return "检测到数据目录被占用，请关闭相关程序后重试。";
            }

            return string.Join("\n", lines);
        }


        private async Task ApplyDataRootMoveAsync(string folderPath)
        {
            if (!EnsureTargetDataRootEmpty(folderPath))
            {
                return;
            }

            await MainView.PrepareCacheForDataRootMoveAsync();
            string currentPath = AppDataController.DataRootPath;

            bool canMove = await CheckDataRootLockedBeforeMoveAsync(currentPath, folderPath);
            if (!canMove)
            {
                return;
            }

            WaitOverlayManager.RaiseWaitOverlay(true, "正在移动数据目录", "正在复制并校验数据\n请不要关闭 WaveTools", true, 0);

            try
            {
                string errorMessage = null;
                bool success = await Task.Run(() => AppDataController.MoveToCustomDataRoot(folderPath, out errorMessage));

                if (!success)
                {
                    throw new InvalidOperationException(errorMessage ?? "移动数据目录失败");
                }

                LoadSettings();
                NotificationManager.RaiseNotification("移动数据目录完成\n旧目录已在校验通过后删除", "", InfoBarSeverity.Success, true, 3);
            }
            catch (Exception ex)
            {
                NotificationManager.RaiseNotification("移动数据目录失败", ex.Message, InfoBarSeverity.Error);
            }
            finally
            {
                WaitOverlayManager.RaiseWaitOverlay(false);
            }
        }

        private async Task ApplyDefaultDataRootMoveAsync()
        {
            string defaultPath = AppDataController.DefaultDataRootPath;

            if (!AppDataController.IsCurrentDataRoot(defaultPath) && !EnsureTargetDataRootEmpty(defaultPath))
            {
                return;
            }

            await MainView.PrepareCacheForDataRootMoveAsync();
            string currentPath = AppDataController.DataRootPath;

            if (!AppDataController.IsCurrentDataRoot(defaultPath))
            {
                bool canMove = await CheckDataRootLockedBeforeMoveAsync(currentPath, defaultPath);
                if (!canMove)
                {
                    return;
                }
            }

            WaitOverlayManager.RaiseWaitOverlay(true, "正在恢复默认数据目录", "正在复制并校验数据\n请不要关闭 WaveTools", true, 0);

            try
            {
                string errorMessage = null;
                bool success = await Task.Run(() => AppDataController.MoveToDefaultDataRoot(out errorMessage));

                if (!success)
                {
                    throw new InvalidOperationException(errorMessage ?? "恢复默认数据目录失败");
                }

                LoadSettings();
                NotificationManager.RaiseNotification("数据目录已恢复\n旧目录已在校验通过后删除", "", InfoBarSeverity.Success, true, 3);
            }
            catch (Exception ex)
            {
                NotificationManager.RaiseNotification("恢复默认数据目录失败", ex.Message, InfoBarSeverity.Error);
            }
            finally
            {
                WaitOverlayManager.RaiseWaitOverlay(false);
            }
        }

        private async Task<string> PickFolderAsync()
        {
            FolderPicker picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            IntPtr hwnd = IntPtr.Zero;
            if (App.MainWindow != null)
            {
                hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            }

            if (hwnd == IntPtr.Zero)
            {
                hwnd = CommonHelpers.FileHelpers.GetActiveWindow();
            }

            if (hwnd != IntPtr.Zero)
            {
                InitializeWithWindow.Initialize(picker, hwnd);
            }

            StorageFolder folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }


        private void StorageManagementHeader_Click(object sender, RoutedEventArgs e)
        {
            SetStorageManagementExpanded(!isStorageManagementExpanded);
        }

        private void SetStorageManagementExpanded(bool isExpanded)
        {
            if (storageManagementContentPanel == null)
            {
                isStorageManagementExpanded = isExpanded;
                return;
            }

            if (isStorageManagementExpanded == isExpanded &&
                storageManagementContentPanel.Visibility == (isExpanded ? Visibility.Visible : Visibility.Collapsed))
            {
                return;
            }

            storageManagementStoryboard?.Stop();
            storageManagementStoryboard = null;

            double fromHeight;
            double toHeight;

            if (isExpanded)
            {
                storageManagementContentPanel.Visibility = Visibility.Visible;
                storageManagementContentPanel.IsHitTestVisible = true;

                fromHeight = storageManagementContentPanel.MaxHeight;
                if (double.IsInfinity(fromHeight) || double.IsNaN(fromHeight) || fromHeight <= 0)
                {
                    fromHeight = 0;
                }

                storageManagementContentPanel.MaxHeight = fromHeight;
                toHeight = GetStorageManagementContentDesiredHeight();
            }
            else
            {
                storageManagementContentPanel.IsHitTestVisible = false;

                fromHeight = storageManagementContentPanel.ActualHeight;
                if (double.IsNaN(fromHeight) || fromHeight <= 0)
                {
                    fromHeight = GetStorageManagementContentDesiredHeight();
                }

                storageManagementContentPanel.MaxHeight = fromHeight;
                toHeight = 0;
            }

            isStorageManagementExpanded = isExpanded;

            TimeSpan layoutDuration = TimeSpan.FromMilliseconds(isExpanded ? 185 : 135);
            TimeSpan visualDuration = TimeSpan.FromMilliseconds(isExpanded ? 150 : 110);

            EasingFunctionBase layoutEasing = isExpanded
                ? new ExponentialEase { Exponent = 7, EasingMode = EasingMode.EaseOut }
                : new ExponentialEase { Exponent = 6, EasingMode = EasingMode.EaseIn };

            EasingFunctionBase visualEasing = new CubicEase
            {
                EasingMode = isExpanded ? EasingMode.EaseOut : EasingMode.EaseIn
            };

            storageManagementStoryboard = new Storyboard();

            AddDoubleAnimation(
                storageManagementStoryboard,
                storageManagementContentPanel,
                "MaxHeight",
                toHeight,
                layoutDuration,
                layoutEasing,
                true);

            AddDoubleAnimation(
                storageManagementStoryboard,
                storageManagementContentPanel,
                "Opacity",
                isExpanded ? 1 : 0,
                visualDuration,
                visualEasing,
                false);

            if (storageManagementContentTranslate != null)
            {
                AddDoubleAnimation(
                    storageManagementStoryboard,
                    storageManagementContentTranslate,
                    "Y",
                    isExpanded ? 0 : -10,
                    visualDuration,
                    visualEasing,
                    false);
            }

            if (storageManagementChevronRotate != null)
            {
                AddDoubleAnimation(
                    storageManagementStoryboard,
                    storageManagementChevronRotate,
                    "Angle",
                    isExpanded ? 180 : 0,
                    TimeSpan.FromMilliseconds(120),
                    visualEasing,
                    false);
            }

            storageManagementStoryboard.Completed += StorageManagementStoryboard_Completed;
            storageManagementStoryboard.Begin();
        }

        private double GetStorageManagementContentDesiredHeight()
        {
            if (storageManagementContentPanel == null)
            {
                return 0;
            }

            double availableWidth = storageManagementContentPanel.ActualWidth;
            if (double.IsNaN(availableWidth) || availableWidth <= 0)
            {
                availableWidth = ActualWidth;
            }

            if (double.IsNaN(availableWidth) || availableWidth <= 0)
            {
                availableWidth = 720;
            }

            double oldMaxHeight = storageManagementContentPanel.MaxHeight;
            Visibility oldVisibility = storageManagementContentPanel.Visibility;

            storageManagementContentPanel.Visibility = Visibility.Visible;
            storageManagementContentPanel.MaxHeight = double.PositiveInfinity;
            storageManagementContentPanel.Measure(new Size(availableWidth, double.PositiveInfinity));

            double desiredHeight = Math.Ceiling(storageManagementContentPanel.DesiredSize.Height);

            storageManagementContentPanel.MaxHeight = oldMaxHeight;
            storageManagementContentPanel.Visibility = oldVisibility;

            return Math.Max(1, desiredHeight);
        }

        private void AddDoubleAnimation(
            Storyboard storyboard,
            DependencyObject target,
            string targetProperty,
            double to,
            TimeSpan duration,
            EasingFunctionBase easingFunction,
            bool enableDependentAnimation)
        {
            DoubleAnimation animation = new DoubleAnimation
            {
                To = to,
                Duration = new Duration(duration),
                EasingFunction = easingFunction,
                EnableDependentAnimation = enableDependentAnimation
            };

            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, targetProperty);
            storyboard.Children.Add(animation);
        }

        private void StorageManagementStoryboard_Completed(object sender, object e)
        {
            if (storageManagementContentPanel == null)
            {
                return;
            }

            if (!isStorageManagementExpanded)
            {
                storageManagementContentPanel.Visibility = Visibility.Collapsed;
                storageManagementContentPanel.MaxHeight = 0;
                storageManagementContentPanel.Opacity = 0;
                storageManagementContentPanel.IsHitTestVisible = false;

                if (storageManagementContentTranslate != null)
                {
                    storageManagementContentTranslate.Y = -10;
                }
            }
            else
            {
                storageManagementContentPanel.Visibility = Visibility.Visible;
                storageManagementContentPanel.MaxHeight = double.PositiveInfinity;
                storageManagementContentPanel.Opacity = 1;
                storageManagementContentPanel.IsHitTestVisible = true;

                if (storageManagementContentTranslate != null)
                {
                    storageManagementContentTranslate.Y = 0;
                }
            }
        }

        private async Task RefreshStorageUsageAsync()
        {
            try
            {
                if (storageTotalSizeText != null)
                {
                    storageTotalSizeText.Text = "正在计算";
                }

                if (cacheUsageText != null)
                {
                    cacheUsageText.Text = "正在计算";
                }

                if (logsUsageText != null)
                {
                    logsUsageText.Text = "正在计算";
                }

                string cachePath = Path.Combine(AppDataController.DataRootPath, "Cache");
                string logsPath = Path.Combine(AppDataController.DataRootPath, "Logs");

                (long cacheSize, long logsSize) = await Task.Run(() =>
                {
                    long cache = GetDirectorySizeSafe(cachePath);
                    long logs = GetDirectorySizeSafe(logsPath);
                    return (cache, logs);
                });

                if (cacheUsageText != null)
                {
                    cacheUsageText.Text = FormatFileSize(cacheSize) + " · " + cachePath;
                }

                if (logsUsageText != null)
                {
                    logsUsageText.Text = FormatFileSize(logsSize) + " · " + logsPath;
                }

                if (storageTotalSizeText != null)
                {
                    storageTotalSizeText.Text = FormatFileSize(cacheSize + logsSize);
                }

                if (clearCacheButton != null)
                {
                    clearCacheButton.IsEnabled = Directory.Exists(cachePath) && cacheSize > 0;
                }

                if (clearLogsButton != null)
                {
                    clearLogsButton.IsEnabled = Directory.Exists(logsPath) && logsSize > 0;
                }
            }
            catch (Exception ex)
            {
                Logging.Write($"Refresh storage usage failed: {ex.Message}", 1);

                if (storageTotalSizeText != null)
                {
                    storageTotalSizeText.Text = "计算失败";
                }

                if (cacheUsageText != null)
                {
                    cacheUsageText.Text = "缓存占用计算失败";
                }

                if (logsUsageText != null)
                {
                    logsUsageText.Text = "日志占用计算失败";
                }
            }
        }

        private static long GetDirectorySizeSafe(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return 0;
            }

            long totalSize = 0;
            Stack<string> directories = new Stack<string>();
            directories.Push(directoryPath);

            while (directories.Count > 0)
            {
                string currentDirectory = directories.Pop();

                string[] files = Array.Empty<string>();
                try
                {
                    files = Directory.GetFiles(currentDirectory);
                }
                catch
                {
                }

                foreach (string file in files)
                {
                    try
                    {
                        totalSize += new FileInfo(file).Length;
                    }
                    catch
                    {
                    }
                }

                string[] subDirectories = Array.Empty<string>();
                try
                {
                    subDirectories = Directory.GetDirectories(currentDirectory);
                }
                catch
                {
                }

                foreach (string subDirectory in subDirectories)
                {
                    directories.Push(subDirectory);
                }
            }

            return totalSize;
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes <= 0)
            {
                return "0 B";
            }

            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unitIndex = 0;

            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }

            if (unitIndex == 0)
            {
                return bytes + " B";
            }

            return value.ToString("0.##") + " " + units[unitIndex];
        }

        private bool TryBeginStorageOperation()
        {
            if (!StorageOperationSemaphore.Wait(0))
            {
                NotificationManager.RaiseNotification(
                    "清理任务正在运行",
                    "请等待当前清理完成后再进行下一次清理",
                    InfoBarSeverity.Warning,
                    true,
                    3);
                return false;
            }

            if (clearCacheButton != null)
            {
                clearCacheButton.IsEnabled = false;
            }

            if (clearLogsButton != null)
            {
                clearLogsButton.IsEnabled = false;
            }

            return true;
        }

        private async Task EndStorageOperationAsync()
        {
            try
            {
                await RefreshStorageUsageAsync();
            }
            finally
            {
                StorageOperationSemaphore.Release();
            }
        }

        private void Clear_Cache_Click(object sender, RoutedEventArgs e)
        {
            DialogManager.RaiseDialog(
                XamlRoot,
                "清理缓存",
                "将删除 Cache 目录中的临时缓存文件\n首页背景、轮播图和资源图标会在需要时重新生成或重新下载",
                true,
                "开始清理",
                async () => await ClearCacheAsync()
            );
        }

        private void Clear_Logs_Click(object sender, RoutedEventArgs e)
        {
            DialogManager.RaiseDialog(
                XamlRoot,
                "清理日志",
                "将删除 Logs 目录中可释放的日志文件\n当前正在写入的日志可能会被保留",
                true,
                "开始清理",
                async () => await ClearLogsAsync()
            );
        }

        private async Task ClearCacheAsync()
        {
            if (!TryBeginStorageOperation())
            {
                return;
            }

            WaitOverlayManager.RaiseWaitOverlay(true, "正在清理缓存", "正在释放首页资源并清理 Cache 目录", true, 0);

            try
            {
                await MainView.PrepareCacheForDataRootMoveAsync();

                string cachePath = Path.Combine(AppDataController.DataRootPath, "Cache");
                ClearDirectoryResult result = await Task.Run(() => ClearDirectoryContents(cachePath));

                if (result.FailedItems > 0)
                {
                    NotificationManager.RaiseNotification(
                        "缓存已部分清理",
                        $"已删除 {result.DeletedFiles} 个文件\n{result.DeletedDirectories} 个目录\n{result.FailedItems} 项被占用或无权限",
                        InfoBarSeverity.Warning,
                        true,
                        5);
                    return;
                }

                NotificationManager.RaiseNotification(
                    "缓存清理完成",
                    $"已删除 {result.DeletedFiles} 个文件\n{result.DeletedDirectories} 个目录",
                    InfoBarSeverity.Success,
                    true,
                    3);
            }
            catch (Exception ex)
            {
                NotificationManager.RaiseNotification("缓存清理失败", ex.Message, InfoBarSeverity.Error, true, 5);
            }
            finally
            {
                WaitOverlayManager.RaiseWaitOverlay(false);
                await EndStorageOperationAsync();
            }
        }

        private async Task ClearLogsAsync()
        {
            if (!TryBeginStorageOperation())
            {
                return;
            }

            WaitOverlayManager.RaiseWaitOverlay(true, "正在清理日志", "正在清理 Logs 目录中可释放的日志文件", true, 0);

            try
            {
                string logsPath = Path.Combine(AppDataController.DataRootPath, "Logs");
                ClearDirectoryResult result = await Task.Run(() => ClearDirectoryContents(logsPath));

                if (result.FailedItems > 0)
                {
                    NotificationManager.RaiseNotification(
                        "日志已部分清理",
                        $"已删除 {result.DeletedFiles} 个文件\n{result.DeletedDirectories} 个目录\n{result.FailedItems} 项正在使用中",
                        InfoBarSeverity.Warning,
                        true,
                        5);
                    return;
                }

                NotificationManager.RaiseNotification(
                    "日志清理完成",
                    $"已删除 {result.DeletedFiles} 个文件\n{result.DeletedDirectories} 个目录",
                    InfoBarSeverity.Success,
                    true,
                    3);
            }
            catch (Exception ex)
            {
                NotificationManager.RaiseNotification("日志清理失败", ex.Message, InfoBarSeverity.Error, true, 5);
            }
            finally
            {
                WaitOverlayManager.RaiseWaitOverlay(false);
                await EndStorageOperationAsync();
            }
        }

        private sealed class ClearDirectoryResult
        {
            public int DeletedFiles { get; set; }
            public int DeletedDirectories { get; set; }
            public int FailedItems { get; set; }
        }

        private static ClearDirectoryResult ClearDirectoryContents(string directoryPath)
        {
            ClearDirectoryResult result = new ClearDirectoryResult();

            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return result;
            }

            Directory.CreateDirectory(directoryPath);

            foreach (string file in SafeEnumerateFiles(directoryPath))
            {
                try
                {
                    File.SetAttributes(file, System.IO.FileAttributes.Normal);
                    File.Delete(file);
                    result.DeletedFiles++;
                }
                catch
                {
                    result.FailedItems++;
                }
            }

            foreach (string directory in SafeEnumerateDirectories(directoryPath)
                         .OrderByDescending(item => item.Length))
            {
                try
                {
                    if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        Directory.Delete(directory, false);
                        result.DeletedDirectories++;
                    }
                }
                catch
                {
                    result.FailedItems++;
                }
            }

            Directory.CreateDirectory(directoryPath);
            return result;
        }

        private static IEnumerable<string> SafeEnumerateFiles(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                yield break;
            }

            Stack<string> directories = new Stack<string>();
            directories.Push(directoryPath);

            while (directories.Count > 0)
            {
                string currentDirectory = directories.Pop();

                string[] files = Array.Empty<string>();
                try
                {
                    files = Directory.GetFiles(currentDirectory);
                }
                catch
                {
                }

                foreach (string file in files)
                {
                    yield return file;
                }

                string[] subDirectories = Array.Empty<string>();
                try
                {
                    subDirectories = Directory.GetDirectories(currentDirectory);
                }
                catch
                {
                }

                foreach (string subDirectory in subDirectories)
                {
                    directories.Push(subDirectory);
                }
            }
        }

        private static IEnumerable<string> SafeEnumerateDirectories(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                yield break;
            }

            Stack<string> directories = new Stack<string>();
            directories.Push(directoryPath);

            while (directories.Count > 0)
            {
                string currentDirectory = directories.Pop();

                string[] subDirectories = Array.Empty<string>();
                try
                {
                    subDirectories = Directory.GetDirectories(currentDirectory);
                }
                catch
                {
                }

                foreach (string subDirectory in subDirectories)
                {
                    yield return subDirectory;
                    directories.Push(subDirectory);
                }
            }
        }


        private async void Backup_Data(object sender, RoutedEventArgs e)
        {
            DateTime now = DateTime.Now;
            string formattedDate = now.ToString("yyyy_MM_dd_HH_mm_ss");
            var suggestFileName = "WaveTools_Backup_" + formattedDate;
            var fileTypeChoices = new Dictionary<string, List<string>>
            {
                { "WaveTools Backup File", new List<string> { ".WaveToolsBackup" } }
            };
            var defaultExtension = ".WaveToolsBackup";

            string filePath = await CommonHelpers.FileHelpers.SaveFile(suggestFileName, fileTypeChoices, defaultExtension);

            if (filePath != null)
            {
                string startPath = AppDataController.DataRootPath;
                string zipPath = filePath;
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
                ZipFile.CreateFromDirectory(startPath, zipPath);
                NotificationManager.RaiseNotification("备份完成", null, InfoBarSeverity.Success, true, 1);
            }
        }

        private void Restore_Data_Click(object sender, RoutedEventArgs e)
        {
            DialogManager.RaiseDialog(this.XamlRoot, "是否要还原数据？", "还原数据将会清空当前所有数据\n还原成功后会进入首次设置", true, "选择文件", Restore_Data);
        }

        private async void Restore_Data()
        {
            WaitOverlayManager.RaiseWaitOverlay(true, "等待选择还原文件", null, false);

            string filePath = await CommonHelpers.FileHelpers.OpenFile(".WaveToolsBackup");

            if (filePath != null)
            {
                Task.Run(() => ClearAllData_NoClose(null, null, false)).Wait();
                Directory.CreateDirectory(AppDataController.DataRootPath);
                Task.Run(() => ZipFile.ExtractToDirectory(filePath, AppDataController.DataRootPath)).Wait();
                AppDataController.ResetCache();
                await ProcessRun.RestartApp();
            }
            else WaitOverlayManager.RaiseWaitOverlay(false);
        }

        private async void Install_Font_Click(object sender, RoutedEventArgs e)
        {
            Logging.Write("Installing fonts", 0);
            installSFF.IsEnabled = false;
            installSFF_Progress.Visibility = Visibility.Visible;
            var progress = new Progress<double>();

            await InstallFont.InstallSegoeFluentFontAsync(progress);
            installSFF.Content = "安装字体后重启WaveTools即生效";
            installSFF_Progress.Visibility = Visibility.Collapsed;
            Logging.Write("Fonts installed", 0);
        }

        private async void Restart_App(TeachingTip sender, object args)
        {
            Logging.Write("Restarting application", 0);
            await ProcessRun.RestartApp();
        }

        // Debug_Clicks

        private void Debug_GeneralTest_2(object sender, RoutedEventArgs e)
        {
            MemHelper.Release();
        }

        private void Debug_GeneralTest(object sender, RoutedEventArgs e)
        {
            WebViewHelper.RaiseWebViewWindow("https://sdk.mihoyo.com/nap/announcement/index.html?auth_appid=announcement&authkey_ver=1&bundle_id=nap_cn&game=nap&game_biz=nap_cn&lang=zh-cn&level=60&platform=pc&region=prod_gf_cn&sdk_presentation_style=fullscreen&sdk_screen_transparent=true&sign_type=2&uid=100000000&", "绝区零游戏内公告", false, 1141, 647, Script);
        }
        private string Script = "window.onload = function() { console.log('ZenlessTools_Cathced!'); setInterval(() => { if (window.closed) { window.chrome.webview.postMessage('window_closed'); } }, 100); (function() { const originalFunction = window.linkClicked; window.linkClicked = function(...args) { window.chrome.webview.postMessage('announcements_link_clicked'); return originalFunction.apply(this, args); }; })(); const root = document.getElementById('root'); root.style.backgroundRepeat = 'no-repeat'; root.style.backgroundPosition = 'left bottom'; root.style.backgroundSize = 'cover'; const bodyHome = document.getElementsByClassName('home__body--pc'); if (bodyHome.length > 0) { bodyHome[0].style.transform = 'scale(1.27, 1.32)'; } const home = document.getElementsByClassName('home'); if (home.length > 0) { home[0].style.background = 'transparent'; } const closeBtn = document.getElementsByClassName('home__close'); if (closeBtn.length > 0) { closeBtn[0].onclick = function() { window.close(); }; } document.addEventListener('click', function(event) { let target = event.target; while (target && target.tagName !== 'A') { target = target.parentNode; } if (target && target.tagName === 'A') { const href = target.getAttribute('href'); const jsRegex = /javascript:miHoYoGameJSSDK\\.openInBrowser\\('([^']+)'\\)/; const jsMatch = href.match(jsRegex); if (jsMatch && jsMatch[1]) { event.preventDefault(); linkClicked(); return; } const uniwebviewPrefix = 'uniwebview://open_url?url='; if (href.startsWith(uniwebviewPrefix)) { event.preventDefault(); const newUrl = decodeURIComponent(href.replace(uniwebviewPrefix, '')); linkClicked(); return; } } }); };";

        private void Debug_CopyDiagnosticInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StringBuilder builder = new StringBuilder();
                using Process currentProcess = Process.GetCurrentProcess();
                builder.AppendLine("WaveTools Debug Diagnostic");
                builder.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                builder.AppendLine("Version: " + CommonHelpers.AppVersionHelper.GetAppVersion());
                builder.AppendLine("ProcessId: " + currentProcess.Id);
                builder.AppendLine("Is64BitProcess: " + Environment.Is64BitProcess);
                builder.AppendLine("OSVersion: " + Environment.OSVersion);
                builder.AppendLine("DebuggerAttached: " + Debugger.IsAttached);
                builder.AppendLine("SModeDebug: " + App.SDebugMode);
                builder.AppendLine("GlobalDebug: " + App.GDebugMode);
                builder.AppendLine("DataRootPath: " + AppDataController.DataRootPath);
                builder.AppendLine("DefaultDataRootPath: " + AppDataController.DefaultDataRootPath);
                builder.AppendLine("UseCustomDataRoot: " + AppDataController.IsUsingCustomDataRoot());
                builder.AppendLine("SettingsFilePath: " + AppDataController.SettingsFilePath);
                builder.AppendLine("BootstrapFilePath: " + AppDataController.BootstrapFilePath);
                builder.AppendLine("GamePath: " + AppDataController.GetGamePath());
                builder.AppendLine("UpdateService: " + AppDataController.GetUpdateService());
                builder.AppendLine("DayNight: " + AppDataController.GetDayNight());
                builder.AppendLine("ConsoleMode: " + AppDataController.GetConsoleMode());
                builder.AppendLine("TerminalMode: " + AppDataController.GetTerminalMode());
                builder.AppendLine("AdminMode: " + AppDataController.GetAdminMode());
                builder.AppendLine("AutoCheckUpdate: " + AppDataController.GetAutoCheckUpdate());
                DataPackage package = new DataPackage();
                package.SetText(builder.ToString());
                Clipboard.SetContent(package);
                NotificationManager.RaiseNotification("诊断信息已复制", "可直接粘贴给开发者排查问题", InfoBarSeverity.Success, true, 2);
            }
            catch (Exception ex)
            {
                NotificationManager.RaiseNotification("复制诊断信息失败", ex.Message, InfoBarSeverity.Error, true, 5);
            }
        }

        private void Debug_OpenDataRoot_Click(object sender, RoutedEventArgs e)
        {
            Debug_OpenDirectory(AppDataController.DataRootPath, true);
        }

        private void Debug_OpenSettingsFile_Click(object sender, RoutedEventArgs e)
        {
            Debug_OpenFileOrDirectory(AppDataController.SettingsFilePath);
        }

        private void Debug_OpenBootstrapFile_Click(object sender, RoutedEventArgs e)
        {
            Debug_OpenFileOrDirectory(AppDataController.BootstrapFilePath);
        }

        private void Debug_OpenLogs_Click(object sender, RoutedEventArgs e)
        {
            Debug_OpenDirectory(AppDataController.GetDataPath("Logs"), true);
        }

        private void Debug_OpenPanic_Click(object sender, RoutedEventArgs e)
        {
            Debug_OpenDirectory(AppDataController.GetDataPath("Panic"), true);
        }

        private void Debug_OpenCache_Click(object sender, RoutedEventArgs e)
        {
            Debug_OpenDirectory(AppDataController.GetDataPath("Cache"), true);
        }

        private void Debug_OpenDepends_Click(object sender, RoutedEventArgs e)
        {
            Debug_OpenDirectory(AppDataController.GetDataPath("Depends"), true);
        }

        private void Debug_WriteLog_Click(object sender, RoutedEventArgs e)
        {
            Logging.Write("Debug log write test", 0);
            NotificationManager.RaiseNotification("测试日志已写入", "请到日志目录查看", InfoBarSeverity.Success, true, 2);
        }

        private void Debug_ReleaseMemory_Click(object sender, RoutedEventArgs e)
        {
            MemHelper.Release();
            NotificationManager.RaiseNotification("内存释放已执行", "已调用 MemHelper.Release", InfoBarSeverity.Success, true, 2);
        }

        private void Debug_ReloadSettings_Click(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            NotificationManager.RaiseNotification("设置已重新加载", "AboutView 已重新读取 settings.json", InfoBarSeverity.Success, true, 2);
        }

        private void Debug_Notification_Info_Test(object sender, RoutedEventArgs e)
        {
            Notification_Test_Count++;
            NotificationManager.RaiseNotification("Info 通知测试", "这是一条Info测试通知" + Notification_Test_Count, InfoBarSeverity.Informational, false, 2);
        }

        private void Debug_Notification_Warning_Test(object sender, RoutedEventArgs e)
        {
            Notification_Test_Count++;
            NotificationManager.RaiseNotification("Warning 通知测试", "这是一条Warning测试通知" + Notification_Test_Count, InfoBarSeverity.Warning, false, 2);
        }

        private void Debug_Notification_Error_Test(object sender, RoutedEventArgs e)
        {
            Notification_Test_Count++;
            NotificationManager.RaiseNotification("Error 通知测试", "这是一条Error测试通知" + Notification_Test_Count, InfoBarSeverity.Error, false, 2);
        }

        private void Debug_OpenFileOrDirectory(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    NotificationManager.RaiseNotification("路径为空", "无法打开空路径", InfoBarSeverity.Warning, true, 3);
                    return;
                }

                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = "/select,\"" + path + "\"",
                        UseShellExecute = true
                    });
                    return;
                }

                string directoryPath = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    Debug_OpenDirectory(directoryPath, false);
                    return;
                }

                NotificationManager.RaiseNotification("路径不存在", path, InfoBarSeverity.Warning, true, 3);
            }
            catch (Exception ex)
            {
                NotificationManager.RaiseNotification("打开路径失败", ex.Message, InfoBarSeverity.Error, true, 5);
            }
        }

        private void Debug_OpenDirectory(string path, bool createIfMissing)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    NotificationManager.RaiseNotification("路径为空", "无法打开空路径", InfoBarSeverity.Warning, true, 3);
                    return;
                }

                if (!Directory.Exists(path))
                {
                    if (createIfMissing)
                    {
                        Directory.CreateDirectory(path);
                    }
                    else
                    {
                        NotificationManager.RaiseNotification("目录不存在", path, InfoBarSeverity.Warning, true, 3);
                        return;
                    }
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                NotificationManager.RaiseNotification("打开目录失败", ex.Message, InfoBarSeverity.Error, true, 5);
            }
        }

        private void Debug_Panic_Click(object sender, RoutedEventArgs e)
        {
            Logging.Write("Triggering global exception handler test", 0);
            throw new Exception("全局异常处理测试");
        }

        private void Debug_Notification_Test(object sender, RoutedEventArgs e)
        {
            Notification_Test_Count++;
            Logging.Write("Triggering notification test", 0);
            NotificationManager.RaiseNotification("测试通知", $"这是一条测试通知{Notification_Test_Count}", InfoBarSeverity.Success, false, 1);
        }

        private async void Debug_WaitOverlayManager_Test(object sender, RoutedEventArgs e)
        {
            Logging.Write("Triggering WaitOverlayManager test", 0);
            WaitOverlayManager.RaiseWaitOverlay(true, "全局等待测试", "如果您看到了这个界面，则全局等待测试已成功", true, 0, true, "退出测试", Debug_KillWaitOverlay);
            await Task.Delay(1000);
            Debug_KillWaitOverlay();
        }

        private void Debug_KillWaitOverlay()
        {
            Logging.Write("Killing WaitOverlay", 0);
            WaitOverlayManager.RaiseWaitOverlay(false);
        }

        private void Debug_ShowDialog_Test(object sender, RoutedEventArgs e)
        {
            Logging.Write("Triggering ShowDialog test", 0);
            DialogManager.RaiseDialog(XamlRoot);
        }

        // Debug_Disable_NavBtns
        private void Debug_Disable_NavBtns(object sender, RoutedEventArgs e)
        {
            Logging.Write("Toggling navigation buttons", 0);
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
            Logging.Write("Getting parent NavigationView", 0);
            DependencyObject parent = VisualTreeHelper.GetParent(child);

            while (parent != null && !(parent is NavigationView))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            return parent as NavigationView;
        }

        private sealed class DataRootLockCheckResult
        {
            public List<LockingProcessInfo> LockingProcesses { get; set; } = new List<LockingProcessInfo>();
            public List<BlockedPathInfo> BlockedPaths { get; set; } = new List<BlockedPathInfo>();

            public bool HasBlockedItems
            {
                get
                {
                    return LockingProcesses.Count > 0 || BlockedPaths.Count > 0;
                }
            }
        }
    }
}
