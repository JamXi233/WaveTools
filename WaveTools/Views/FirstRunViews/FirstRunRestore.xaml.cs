// Copyright (c) 2021-2024, JamXi JSG-LLC.
// All rights reserved.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using WaveTools.Depend;

namespace WaveTools.Views.FirstRunViews
{
    public sealed partial class FirstRunRestore : Page
    {
        public FirstRunRestore()
        {
            InitializeComponent();
            Logging.Write("Switch to FirstRunRestore", 0);
            AppDataController.SetFirstRunStatus(2);
            DataRootText.Text = "当前数据目录：" + AppDataController.DataRootPath;
        }

        private void NextPage(object sender, RoutedEventArgs e)
        {
            GoToNextRequiredPage();
        }

        private async void Restore_Data(object sender, RoutedEventArgs e)
        {
            string filePath = await CommonHelpers.FileHelpers.OpenFile(".WaveToolsBackup");

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                string dataRootPath = AppDataController.DataRootPath;
                DeleteFolder(dataRootPath);
                Directory.CreateDirectory(dataRootPath);

                await Task.Run(() => ZipFile.ExtractToDirectory(filePath, dataRootPath));
                AppDataController.ResetCache();
                AppDataController.EnsureDataRootReady();

                GoToNextRequiredPage();
            }
            catch (Exception ex)
            {
                Logging.Write("Restore data failed: " + ex.Message, 2);
                App.NotificationManager.RaiseNotification("还原数据失败", ex.Message, InfoBarSeverity.Error);
            }
        }

        private void GoToNextRequiredPage()
        {
            Frame parentFrame = GetParentFrame(this);
            FirstRunNavigation.NavigateToNextRequiredPage(parentFrame);
        }

        private void DeleteFolder(string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                try
                {
                    Directory.Delete(folderPath, true);
                }
                catch (IOException)
                {
                    // ignored
                }
            }
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
