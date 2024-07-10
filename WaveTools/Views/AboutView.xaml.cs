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
using WaveTools.Depend;

namespace WaveTools.Views
{
    public sealed partial class AboutView : Page
    {
        private readonly GetGithubLatest _getGithubLatest = new GetGithubLatest();
        private readonly GetJSGLatest _getJSGLatest = new GetJSGLatest();

        private bool isProgrammaticChange = false;

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
            consoleToggle.IsEnabled = !isDebug;
            debug_Mode.Visibility = isDebug ? Visibility.Visible : Visibility.Collapsed;
            debug_Message.Text = App.SDebugMode ? "您现在处于手动Debug模式" : "";
            appVersion.Text = $"WaveTools {Package.Current.Id.Version.Major}.{Package.Current.Id.Version.Minor}.{Package.Current.Id.Version.Build}.{Package.Current.Id.Version.Revision}";
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
            consoleToggle.IsChecked = AppDataController.GetConsoleMode() == 1;
            terminalToggle.IsChecked = AppDataController.GetTerminalMode() == 1;
            autoCheckUpdateToggle.IsChecked = AppDataController.GetAutoCheckUpdate() == 1;
            adminModeToggle.IsChecked = AppDataController.GetAdminMode() == 1;
            userviceRadio.SelectedIndex = AppDataController.GetUpdateService() == 0 ? 1 : AppDataController.GetUpdateService() == 2 ? 0 : -1;
            themeRadio.SelectedIndex = AppDataController.GetDayNight();
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
            var response = await new HttpClient().GetAsync("https://api.jamsg.cn/version");
            if (response.IsSuccessStatusCode)
            {
                var data = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
                apiVersion.Text = "ArrowAPI " + data.arrow_ver;
                antiCatVersion.Text = "AntiCat " + data.anticat_ver;
                Logging.Write($"API Version: {apiVersion.Text}, AntiCat Version: {antiCatVersion.Text}", 0);
            }
            else
            {
                Logging.Write("Failed to get version information", 1);
            }
        }

        private void Console_Toggle(object sender, RoutedEventArgs e)
        {
            Logging.Write("Toggling console mode", 0);
            if (consoleToggle.IsChecked ?? false) TerminalMode.ShowConsole(); else TerminalMode.HideConsole();
            AppDataController.SetConsoleMode(consoleToggle.IsChecked == true ? 1 : 0);
        }

        private void TerminalMode_Toggle(object sender, RoutedEventArgs e)
        {
            Logging.Write("Toggling terminal mode", 0);
            TerminalTip.IsOpen = terminalToggle.IsChecked ?? false;
            AppDataController.SetTerminalMode(terminalToggle.IsChecked == true ? 1 : 0);
        }

        private void Auto_Check_Update_Toggle(object sender, RoutedEventArgs e)
        {
            AppDataController.SetAutoCheckUpdate(autoCheckUpdateToggle.IsChecked == true ? 1 : 0);
        }

        private void Admin_Mode_Toggle(object sender, RoutedEventArgs e)
        {
            AppDataController.SetAdminMode(adminModeToggle.IsChecked == true ? 1 : 0);
        }

        public void Clear_AllData_TipShow(object sender, RoutedEventArgs e)
        {
            Logging.Write("Showing Clear All Data Tip", 0);
            ClearAllDataTip.IsOpen = true;
        }

        public async void ClearAllData(TeachingTip sender, object args)
        {
            Logging.Write("Clearing all data", 0);
            string userDocumentsFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string targetFolderPath = Path.Combine(userDocumentsFolderPath, "JSG-LLC", "WaveTools");
            await DeleteFolderAsync(targetFolderPath, true);
        }

        public async void ClearAllData_NoClose(object sender, RoutedEventArgs e, bool close = false)
        {
            Logging.Write("Clearing all data without closing", 0);
            string userDocumentsFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string targetFolderPath = Path.Combine(userDocumentsFolderPath, "JSG-LLC", "WaveTools");
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
            var localFolder = ApplicationData.Current.LocalFolder;
            await DeleteFilesAndSubfoldersAsync(localFolder);

            // 清除应用的本地数据
            await ApplicationData.Current.ClearAsync(ApplicationDataLocality.Local);

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
            var result = await GetUpdate.GetWaveToolsUpdate();
            var status = result.Status;
            UpdateTip.Target = checkUpdate;
            UpdateTip.ActionButtonClick -= StartDependForceUpdate;
            UpdateTip.ActionButtonClick -= StartForceUpdate;
            UpdateTip.ActionButtonClick -= DisplayUpdateInfo;
            bool isShiftPressed = (GetAsyncKeyState(0x10) & 0x8000) != 0;

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
            var result = await GetUpdate.GetDependUpdate();
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

        public async void DisplayUpdateInfo(TeachingTip sender, object args)
        {
            Logging.Write("Displaying update information", 0);
            bool isWaveTools = UpdateTip.Target != checkDependUpdate;
            UpdateResult updateinfo = isWaveTools ? await GetUpdate.GetWaveToolsUpdate() : await GetUpdate.GetDependUpdate();
            UpdateTip.IsOpen = false;
            var Title = (isWaveTools ? "WaveTools" : "Helper") + ":" + updateinfo.Version + "版本可用";
            var Content = "更新日志:\n" + updateinfo.Changelog;
            var CloseButtonText = "关闭";
            var PrimaryButtonText = "立即更新";
            var DefaultButton = ContentDialogButton.Primary;
            var XamlRoot = sender.XamlRoot;
            Action action;
            if (isWaveTools) action = StartUpdate; else action = StartDependUpdate;
            
            DialogManager.RaiseDialog(XamlRoot,Title,Content,true,PrimaryButtonText, action);
        }

        public async void StartUpdate()
        {
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
        private void ThemeRadio_Follow(object sender, RoutedEventArgs e)
        {
            Logging.Write("Selected theme: Follow", 0);
            if (!isProgrammaticChange) { ThemeTip.IsOpen = true; AppDataController.SetDayNight(0); }
        }

        private void ThemeRadio_Light(object sender, RoutedEventArgs e)
        {
            Logging.Write("Selected theme: Light", 0);
            if (!isProgrammaticChange) { ThemeTip.IsOpen = true; AppDataController.SetDayNight(1); }
        }

        private void ThemeRadio_Dark(object sender, RoutedEventArgs e)
        {
            Logging.Write("Selected theme: Dark", 0);
            if (!isProgrammaticChange) { ThemeTip.IsOpen = true; AppDataController.SetDayNight(2); }
        }

        // 选择下载渠道开始
        private void uservice_Github_Choose(object sender, RoutedEventArgs e)
        {
            Logging.Write("Selected update service: Github", 0);
            if (!isProgrammaticChange) { AppDataController.SetUpdateService(0); }
        }

        private void uservice_Gitee_Choose(object sender, RoutedEventArgs e)
        {
            Logging.Write("Selected update service: Gitee", 0);
            if (!isProgrammaticChange) { AppDataController.SetUpdateService(1); }
        }

        private void uservice_JSG_Choose(object sender, RoutedEventArgs e)
        {
            Logging.Write("Selected update service: JSG", 0);
            if (!isProgrammaticChange) { AppDataController.SetUpdateService(2); }
        }

        private async void Backup_Data(object sender, RoutedEventArgs e)
        {
            Logging.Write("Starting data backup", 0);
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
                    Logging.Write($"Deleted existing file: {zipPath}", 0);
                }
                ZipFile.CreateFromDirectory(startPath, zipPath);
                Logging.Write($"Backup created at: {zipPath}", 0);
            }
            else
            {
                Logging.Write("No file selected for backup", 1);
            }
        }

        private void Restore_Data_Click(object sender, RoutedEventArgs e)
        {
            Logging.Write("Showing Restore Data Tip", 0);
            RestoreTip.IsOpen = true;
        }

        private async void Restore_Data(TeachingTip sender, object args)
        {
            Logging.Write("Starting data restore", 0);
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
                        Logging.Write($"Restored data from: {file.Path}", 0);
                    }
                    catch (Exception ex)
                    {
                        Logging.Write($"Error during restore process: {ex.Message}", 1);
                        Debug.WriteLine("Error during restore process: " + ex.Message);
                        sender.Subtitle = "Restore failed: " + ex.Message;
                        sender.IsOpen = true;
                        return;
                    }

                    // 重启应用
                    await ProcessRun.RestartApp();
                }
                else
                {
                    Logging.Write("No file selected for restore", 1);
                }
            }
            finally
            {
                window.Close();  // 确保Window在结束时关闭，释放资源
            }
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
        private void Debug_Panic_Click(object sender, RoutedEventArgs e) 
        {
            Logging.Write("Triggering global exception handler test", 0);
            throw new Exception("全局异常处理测试");
        }

        private void Debug_Notification_Test(object sender, RoutedEventArgs e)
        {
            Notification_Test_Count++;
            Logging.Write("Triggering notification test", 0);
            NotificationManager.RaiseNotification("测试通知",$"这是一条测试通知{Notification_Test_Count}", InfoBarSeverity.Success, false, 1);
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
    }
}
