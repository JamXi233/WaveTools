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

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;

namespace WaveTools.Depend
{
    class InstallFont
    {

        public static async Task<int> InstallSegoeFluentFontAsync(IProgress<double> progress)
        {
            string updateFileFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JSG-LLC", "Fonts");
            string updateFileName = "SegoeFluentIcons.zip";
            string localFilePath = Path.Combine(updateFileFolder, updateFileName);
            string fontName = "Segoe Fluent Icons.ttf";

            // 确保字体文件夹存在
            Directory.CreateDirectory(updateFileFolder);

            // 下载字体文件
            try
            {
                var getNetData = new GetNetData();
                await getNetData.DownloadFileWithProgressAsync("https://aka.ms/SegoeFluentIcons", localFilePath, progress);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"下载字体时出错: {ex.Message}");
                return 1; // 表示下载失败
            }

            // 解压字体文件
            string tempFolder = Path.Combine(updateFileFolder, "temp");

            // 确保临时文件夹不存在，如果存在则删除
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, true);
            }

            // 重新创建临时文件夹
            Directory.CreateDirectory(tempFolder);

            try
            {
                ZipFile.ExtractToDirectory(localFilePath, tempFolder);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"解压时出错: {ex.Message}");
                // 可以选择在这里处理错误或返回
                return 1;
            }

            // 获取字体文件路径
            string fontFilePath = Path.Combine(tempFolder, fontName);

            if (!File.Exists(fontFilePath))
            {
                Console.WriteLine("字体文件不存在.");
                return 1; // 字体文件未找到
            }

            // 打开字体文件以供用户安装
            Process.Start("explorer", fontFilePath);

            App.WaitOverlayManager.RaiseWaitOverlay(true, "请点击安装", "安装后需要重启工具箱来生效", false, 0);

            return 0; // 表示字体文件已打开等待用户操作
        }
    }
}
