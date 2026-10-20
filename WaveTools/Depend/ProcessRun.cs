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

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using WaveTools.Depend;
using static WaveTools.App;

namespace WaveTools.Depend
{
    class ProcessRun
    {
        [DllImport("kernel32.dll")]
        private static extern uint GetOEMCP();

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private static async Task<string> ReadHelperOutputAsync(Stream stream)
        {
            using (var buffer = new MemoryStream())
            {
                await stream.CopyToAsync(buffer);
                byte[] bytes = buffer.ToArray();

                try
                {
                    return StrictUtf8.GetString(bytes).TrimStart('\uFEFF');
                }
                catch (DecoderFallbackException)
                {
                    // Older .NET Framework helpers use the Windows OEM code page.
                    Encoding encoding = CodePagesEncodingProvider.Instance.GetEncoding((int)GetOEMCP());
                    if (encoding == null)
                    {
                        throw new InvalidOperationException("无法识别 WaveToolsHelper 的输出编码。");
                    }
                    return encoding.GetString(bytes);
                }
            }
        }

        public static async Task<string> WaveToolsHelperAsync(string args, bool throwOnFailure = false)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    using (Process process = new Process())
                    {
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                        process.StartInfo.FileName = AppDataController.GetDataPath("Depends", "WaveToolsHelper", "WaveToolsHelper.exe");
                        process.StartInfo.Arguments = args;

                        if (!string.IsNullOrWhiteSpace(AppDataController.DataRootPath))
                        {
                            process.StartInfo.EnvironmentVariables["WAVETOOLS_DATA_ROOT"] = AppDataController.DataRootPath;
                        }

                        Logging.Write($"Starting process: {process.StartInfo.FileName} with arguments: {args}", 0, "WaveToolsHelper");

                        process.Start();

                        Task<string> outputTask = ReadHelperOutputAsync(process.StandardOutput.BaseStream);
                        Task<string> errorTask = ReadHelperOutputAsync(process.StandardError.BaseStream);
                        await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync());
                        string output = await outputTask;
                        string error = await errorTask;

                        if (!string.IsNullOrEmpty(error))
                        {
                            Logging.Write($"Error: {error}", 3, "WaveToolsHelper");
                        }

                        if (throwOnFailure && process.ExitCode != 0)
                        {
                            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                                ? $"WaveToolsHelper 执行失败（{process.ExitCode}）。"
                                : error.Trim());
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
                NotificationManager.RaiseNotification("错误", "停止WaveToolsHelper失败" + ex.ToString(), InfoBarSeverity.Error);
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
            foreach (var process in Process.GetProcessesByName("Wuthering Waves"))
            {
                process.Kill();
            }
        }

        public async static Task RestartApp()
        {
            Logging.Write("Restart WaveTools Requested", 2);
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

        public static void RequestAdminAndRestart()
        {
            Logging.Write("Restart WaveTools Requested", 2);
            var processId = Process.GetCurrentProcess().Id;
            var fileName = Process.GetCurrentProcess().MainModule.FileName;
            ProcessStartInfo info = new ProcessStartInfo(fileName)
            {
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(info);
            Process.GetProcessById(processId).Kill();
        }

        public static bool IsRunAsAdmin()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

    }
}
