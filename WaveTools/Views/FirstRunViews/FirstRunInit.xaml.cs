// Copyright (c) 2021-2024, JamXi JSG-LLC.
// All rights reserved.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;
using WaveTools.Depend;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace WaveTools.Views.FirstRunViews
{
    public sealed partial class FirstRunInit : Page
    {
        public FirstRunInit()
        {
            InitializeComponent();
            Logging.Write("Switch to FirstRunInit", 0);
            CustomPathText.Text = "未选择目录";
        }

        private void UseDefaultLocation_Click(object sender, RoutedEventArgs e)
        {
            bool hasExistingWaveToolsData = AppDataController.HasExistingWaveToolsData(AppDataController.DefaultDataRootPath, false);

            if (!AppDataController.UseDefaultDataRoot(!hasExistingWaveToolsData, out string errorMessage))
            {
                ShowError(errorMessage);
                return;
            }

            bool hasReusableFirstRunData = HasReusableFirstRunData(hasExistingWaveToolsData);

            PrepareSelectedDataRoot();
            GoToNextPage(hasReusableFirstRunData);
        }

        private async void UseCustomLocation_Click(object sender, RoutedEventArgs e)
        {
            string folderPath = await PickFolderAsync();
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            bool acceptAnySettingsFile = !AppDataController.IsCurrentDataRoot(folderPath);
            bool hasExistingWaveToolsData = AppDataController.HasExistingWaveToolsData(folderPath, acceptAnySettingsFile);

            if (!AppDataController.UseCustomDataRoot(folderPath, !hasExistingWaveToolsData, out string errorMessage))
            {
                ShowError(errorMessage);
                return;
            }

            CustomPathText.Text = folderPath;
            bool hasReusableFirstRunData = HasReusableFirstRunData(hasExistingWaveToolsData);

            PrepareSelectedDataRoot();
            GoToNextPage(hasReusableFirstRunData);
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

            Windows.Storage.StorageFolder folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }

        private void PrepareSelectedDataRoot()
        {
            AppDataController appDataController = new AppDataController();
            appDataController.FirstRunInit();
        }

        private bool HasReusableFirstRunData(bool hasExistingWaveToolsData)
        {
            return hasExistingWaveToolsData ||
                   AppDataController.HasThemeSetting() ||
                   AppDataController.HasUpdateServiceSetting() ||
                   AppDataController.HasWaveToolsHelper();
        }

        private void GoToNextPage(bool hasExistingWaveToolsData)
        {
            if (hasExistingWaveToolsData)
            {
                GoToNextRequiredPage();
                return;
            }

            GoToRestorePage();
        }

        private void GoToRestorePage()
        {
            AppDataController.SetFirstRunStatus(2);

            Frame parentFrame = GetParentFrame(this);
            if (parentFrame != null)
            {
                parentFrame.Navigate(typeof(FirstRunRestore));
            }
        }

        private void GoToNextRequiredPage()
        {
            AppDataController.SetFirstRun(1);

            Frame parentFrame = GetParentFrame(this);
            FirstRunNavigation.NavigateToNextRequiredPage(parentFrame);
        }

        private void ShowError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                message = "设置数据目录失败。";
            }

            App.NotificationManager.RaiseNotification("数据目录不可用", message, InfoBarSeverity.Error);
        }

        private Frame GetParentFrame(FrameworkElement child)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);

            while (parent != null && !(parent is Frame))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            return parent as Frame;
        }
    }
}
