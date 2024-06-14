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

using System;
using Microsoft.UI.Xaml.Controls;
using WaveTools.Depend;
using Windows.Storage;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using SRTools.Depend;
using System.IO;

namespace WaveTools.Views.NotifyViews
{
    public sealed partial class NotifyNotificationView : Page
    {
        List<String> list = new List<String>();
        public NotifyNotificationView()
        {
            this.InitializeComponent();
            Logging.Write("Switch to NotifyActivityView", 0);

            // 获取用户文档目录下的JSG-LLC\WaveTools\Posts目录
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string waveToolsFolderPath = Path.Combine(documentsPath, "JSG-LLC", "WaveTools", "Posts");
            string settingsFilePath = Path.Combine(waveToolsFolderPath, "activity.json");

            // 确保目录和文件存在
            if (File.Exists(settingsFilePath))
            {
                string notify = File.ReadAllText(settingsFilePath);
                GetNotify getNotify = new GetNotify();
                var records = getNotify.GetData(notify);
                NotifyNotificationView_List.ItemsSource = records;
                LoadData(records);
            }
            else
            {
                Logging.Write("Notice file not found", 0);
            }
        }

        private void LoadData(List<GetNotify> getNotifies)
        {
            foreach (GetNotify getNotify in getNotifies)
            {
                list.Add(getNotify.jumpUrl);
            }
        }

        private async void List_PointerPressed(object sender, ItemClickEventArgs e)
        {
            await Task.Delay(TimeSpan.FromSeconds(0.1));
            string url = list[NotifyNotificationView_List.SelectedIndex]; // 替换为要打开的网页地址
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            await Task.Delay(TimeSpan.FromSeconds(0.1));
            NotifyNotificationView_List.SelectedIndex = -1;
        }

    }
}