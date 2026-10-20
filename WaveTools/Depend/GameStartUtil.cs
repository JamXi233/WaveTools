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
using static WaveTools.App;

namespace WaveTools.Depend
{
    internal class GameStartUtil
    {
        public void StartGame()
        {
            string gamePath = AppDataController.GetGamePath();
            if (!File.Exists(gamePath))
            {
                NotificationManager.RaiseNotification("无法启动游戏", "游戏本体不存在，请重新选择路径。",
                    Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning, false, 5);
                return;
            }
            var installation = GameResourceQuality.Detect(gamePath);
            if (installation.RequiresArgument && installation.Options.Count == 0)
            {
                Logging.Write(installation.Error, 1);
                NotificationManager.RaiseNotification("无法启动游戏", installation.Error,
                    Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning, false, 8);
                return;
            }
            string arguments = AppDataController.GetGameParameter();
            string quality = GameResourceQuality.Select(installation,
                AppDataController.GetGameResourceQuality(gamePath), arguments);
            string Parameter = GameResourceQuality.BuildArguments(arguments, quality, AppDataController.GetDX11Enable() == 1);
            if (quality != null) AppDataController.SetGameResourceQuality(gamePath, quality);

            string gameDirectory = Path.GetDirectoryName(gamePath);

            var processInfo = new ProcessStartInfo(gamePath)
            {
                Arguments = Parameter,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = gameDirectory
            };

            Process.Start(processInfo);
        }

        public void StartLauncher()
        {
            string gamePath = AppDataController.GetGamePath();
            var processInfo = new ProcessStartInfo(gamePath.Replace("Wuthering Waves.exe", "..\\launcher.exe"));

            processInfo.UseShellExecute = true;
            processInfo.Verb = "runas";
            Process.Start(processInfo);
        }
    }
}
