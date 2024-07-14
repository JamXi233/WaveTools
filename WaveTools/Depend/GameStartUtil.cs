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
using System.Diagnostics;
using System.IO;
using Windows.Storage;

namespace WaveTools.Depend
{
    internal class GameStartUtil
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        public async void StartGame()
        {
            string userDocumentsFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string gamePath = localSettings.Values["Config_GamePath"] as string;

            // 获取游戏的执行路径（目录）
            string gameDirectory = Path.GetDirectoryName(gamePath);

            var processInfo = new ProcessStartInfo(gamePath)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = gameDirectory // 设置当前路径为执行路径
            };

            // 启动程序
            Process.Start(processInfo);
        }

        public void StartLauncher()
        {
            string gamePath = localSettings.Values["Config_GamePath"] as string;
            var processInfo = new ProcessStartInfo(gamePath.Replace("Wuthering Waves.exe", "..\\launcher.exe"));

            //启动程序
            processInfo.UseShellExecute = true;
            processInfo.Verb = "runas";
            Process.Start(processInfo);
        }
    }
}
