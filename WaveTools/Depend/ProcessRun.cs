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
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using SRTools.Depend;
using static WaveTools.App;

namespace WaveTools.Depend
{
    class ProcessRun
    {
        public static async Task<string> WaveToolsHelperAsync(string args)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (Process process = new Process())
                    {
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true; // 捕获标准错误输出
                        process.StartInfo.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"JSG-LLC\WaveTools\Depends\WaveToolsHelper\WaveToolsHelper.exe");
                        process.StartInfo.Arguments = args;

                        Logging.Write($"Starting process: {process.StartInfo.FileName} with arguments: {args}", 0, "WaveToolsHelper");

                        process.Start();


                        // 同时读取标准输出和标准错误
                        string output = process.StandardOutput.ReadToEnd();
                        string error = process.StandardError.ReadToEnd();

                        process.WaitForExit();

                        if (!string.IsNullOrEmpty(error))
                        {
                            Logging.Write($"Error: {error}", 3, "WaveToolsHelper");
                        }

                        Logging.Write(output.Trim(), 3, "WaveToolsHelper");
                        return output.Trim();
                    }
                }
                catch (Exception ex)
                {
                    Logging.Write($"Exception in WaveToolsHelperAsync: {ex.Message}", 3, "WaveToolsHelper");
                    throw;
                }
            });
        }

        public static void StopWaveToolsHelperProcess()
        {
            try
            {
                foreach (var process in Process.GetProcessesByName("WaveToolsHelper"))
                {
                    process.Kill();
                }
                NotificationManager.RaiseNotification("WaveToolsHelper", "已停止依赖运行", InfoBarSeverity.Warning);
            }
            catch (Exception ex)
            {
                NotificationManager.RaiseNotification("错误", "停止WaveToolsHelper失败"+ex.ToString(),InfoBarSeverity.Error);
            }
        }

        public static void StopWaveProcess()
        {
            foreach (var process in Process.GetProcessesByName("Client-Win64-Shipping"))
            {
                process.Kill();
            }
            foreach (var process in Process.GetProcessesByName("KRSDKExternal"))
            {
                process.Kill();
            }
            foreach (var process in Process.GetProcessesByName("Wuthering Waves.exe"))
            {
                process.Kill();
            }
        }



        public async static Task RestartApp()
        {
            Logging.Write("Restart WaveTools Requested",2);
            var processId = Process.GetCurrentProcess().Id;
            var fileName = Process.GetCurrentProcess().MainModule.FileName;
            ProcessStartInfo info = new ProcessStartInfo(fileName)
            {
                UseShellExecute = true,
            };
            Process.Start(info);
            await Task.Delay(100);
            Process.GetProcessById(processId).Kill();
        } 
    }
}
