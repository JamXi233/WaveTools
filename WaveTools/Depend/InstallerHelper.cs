// Copyright (c) 2021-2026, JamXi JSG-LLC.
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
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace WaveTools.Depend
{
    public class InstallerHelper
    {
        private static readonly string BaseInstallerPath = AppDataController.GetDataPath("Installer");
        private static readonly string InstallerFileName = "WaveToolsInstaller.exe";
        private static readonly string InstallerFullPath = Path.Combine(BaseInstallerPath, InstallerFileName);
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
                    string downloadLink = installerInfo.link;

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

        public static int RunInstaller(string args = "")
        {
            if (!File.Exists(InstallerFullPath))
            {
                Logging.Write("安装程序不存在，请先下载。", 1);
                return -1;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = InstallerFullPath,
                Arguments = AppendDataRootArgument(args),
                UseShellExecute = true,
                Verb = "runas"
            };

            try
            {
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        Logging.Write("安装程序启动失败。", 2);
                        return -2;
                    }

                    process.WaitForExit();

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

        private static string AppendDataRootArgument(string args)
        {
            string dataRootPath = AppDataController.DataRootPath;

            if (string.IsNullOrWhiteSpace(dataRootPath))
            {
                return args ?? string.Empty;
            }

            string result = args ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(result))
            {
                result += " ";
            }

            return result + "/DataRoot " + QuoteArgument(dataRootPath);
        }

        private static string QuoteArgument(string value)
        {
            if (value == null)
            {
                return "\"\"";
            }

            StringBuilder builder = new StringBuilder();
            builder.Append('"');

            foreach (char c in value)
            {
                if (c == '"' || c == '\\')
                {
                    builder.Append('\\');
                }

                builder.Append(c);
            }

            builder.Append('"');
            return builder.ToString();
        }
    }
}
