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

using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using WaveTools.Depend;
using Microsoft.UI.Xaml;
using System;
using Windows.Storage.Pickers;
using System.IO;
using System.IO.Compression;
using Windows.Storage;
using Microsoft.UI.Xaml.Media;
using System.Diagnostics;

namespace WaveTools.Views.FirstRunViews
{
    public sealed partial class FirstRunInit : Page
    {
        public FirstRunInit()
        {
            this.InitializeComponent();
            Logging.Write("Switch to FirstRunInit", 0);
            AppDataController.SetFirstRunStatus(1);
        }

        private void NextPage(object sender, RoutedEventArgs e)
        {
            Frame parentFrame = GetParentFrame(this);
            if (parentFrame != null)
            {
                // 前往下载依赖页面
                parentFrame.Navigate(typeof(FirstRunTheme));
            }
        }

        private async void Restore_Data(object sender, RoutedEventArgs e)
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
                    DeleteFolder(userDocumentsFolderPath + "\\JSG-LLC\\WaveTools\\", "0");
                    try
                    {
                        await Task.Run(() => ZipFile.ExtractToDirectory(file.Path, userDocumentsFolderPath + "\\JSG-LLC\\WaveTools\\"));
                    }
                    catch (Exception ex)
                    {
                        // 处理解压缩过程中可能出现的异常
                        Debug.WriteLine("Failed to extract backup: " + ex.Message);
                    }
                    Frame parentFrame = GetParentFrame(this);
                    if (parentFrame != null)
                    {
                        // 前往下载依赖页面
                        parentFrame.Navigate(typeof(FirstRunTheme));
                    }
                }
            }
            finally
            {
                if (window != null)
                {
                    window.Close(); // 确保窗口被关闭
                }
            }
        }

        private void DeleteFolder(string folderPath, String Close)
        {
            if (Directory.Exists(folderPath))
            {
                try { Directory.Delete(folderPath, true); }
                catch (IOException) { }
            }
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