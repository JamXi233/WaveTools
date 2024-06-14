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

using Newtonsoft.Json;
using SRTools.Depend;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace WaveTools.Depend
{
    public class InstallerHelper
    {
        private static readonly string BaseInstallerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JSG-LLC", "WaveTools", "Installer");
        private static string InstallerFileName = ""; 
        private static string InstallerFullPath = Path.Combine(BaseInstallerPath, InstallerFileName);
        private static readonly string InstallerInfoUrl = "https://api.jamsg.cn/release/getversion?package=cn.jamsg.WaveToolsinstaller";

        public static bool CheckInstaller()
        {
            return File.Exists(InstallerFullPath);
        }
        public static async Task GetInstaller()
        {
            using (var httpClient = new HttpClient())
            {
                try
                {
                    string json = await httpClient.GetStringAsync(InstallerInfoUrl);
                    dynamic installerInfo = JsonConvert.DeserializeObject(json);
                    string version = installerInfo.version;
                    string downloadLink = installerInfo.link;

                    InstallerFileName = $"WaveToolsInstaller_{version}.exe";
                    InstallerFullPath = Path.Combine(BaseInstallerPath, InstallerFileName);

                    if (!Directory.Exists(BaseInstallerPath))
                    {
                        Directory.CreateDirectory(BaseInstallerPath);
                    }
                    using (var response = await httpClient.GetAsync(downloadLink))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            using (var fs = new FileStream(InstallerFullPath, FileMode.Create))
                            {
                                await response.Content.CopyToAsync(fs);
                            }
                        }
                        else
                        {
                            throw new Exception("无法下载安装程序");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logging.Write($"下载安装程序时出错: {ex.Message}", 3);
                }
            }
        }

        public static async Task<int> RunInstallerAsync(string args = "")
        {
            if (!File.Exists(InstallerFullPath))
            {
                Logging.Write("安装程序不存在，请先下载。", 1);
                return -1;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = InstallerFullPath,
                Arguments = args,
                UseShellExecute = true,
                Verb = "runas"
            };

            try
            {
                using (Process process = Process.Start(startInfo))
                {
                    await process.WaitForExitAsync();

                    // 检查退出代码
                    if (process.ExitCode != 0)
                    {
                        Logging.Write($"安装程序退出代码: {process.ExitCode}", 2);
                    }

                    return process.ExitCode;
                }
            }
            catch (Exception ex)
            {
                Logging.Write($"运行安装程序时出错: {ex.Message}", 2);
                return -2;
            }
        }

    }
}
