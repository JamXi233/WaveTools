// Copyright (c) 2021-2024, JamXi JSG-LLC.
// All rights reserved.

// This file is part of SRTools.

// SRTools is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// SRTools is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with SRTools.  If not, see <http://www.gnu.org/licenses/>.

// For more information, please refer to <https://www.gnu.org/licenses/gpl-3.0.html>

using Spectre.Console;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace SRTools.Depend
{
    internal class Logging
    {
        private static readonly string LogFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JSG-LLC", "Logs");
        private static readonly string LogFileName = $"WaveTools_Log_{DateTime.Now:yyyyMMdd_HHmmss}.log";
        private static readonly string LogFilePath = Path.Combine(LogFolderPath, LogFileName);

        static Logging()
        {
            try
            {
                Directory.CreateDirectory(LogFolderPath);
                File.Create(LogFilePath).Close();
                string computerInfo = GetComputerInfo();
                Write("Start Logging...");
                Write(computerInfo);
                Console.Clear();
            }
            catch (Exception ex)
            {
                AnsiConsole.Write(new Markup("[bold Red][[ERROR]][/] 初始化日志失败: " + ex.Message));
            }
        }

        private static string GetComputerInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine(@" __        __             _____           _     ");
            sb.AppendLine(@" \ \      / /_ ___   ____|_   _|__   ___ | |___ ");
            sb.AppendLine(@"  \ \ /\ / / _` \ \ / / _ \| |/ _ \ / _ \| / __|");
            sb.AppendLine(@"   \ V  V / (_| |\ V /  __/| | (_) | (_) | \__ \");
            sb.AppendLine(@"    \_/\_/ \__,_| \_/ \___||_|\___/ \___/|_|___/");
            sb.AppendLine();
            sb.AppendLine("Getting System Information...");
            sb.AppendLine("-------------------------------------------SYSTEM INFORMATION-------------------------------------------");
            sb.AppendLine("Operating System: " + Environment.OSVersion);
            sb.AppendLine("Machine Name: " + Environment.MachineName);
            sb.AppendLine("User Name: " + Environment.UserName);
            sb.AppendLine("System Directory: " + Environment.SystemDirectory);
            sb.AppendLine("Processor Count: " + Environment.ProcessorCount);
            sb.AppendLine("64-bit OS: " + Environment.Is64BitOperatingSystem);
            sb.AppendLine("64-bit Process: " + Environment.Is64BitProcess);
            sb.AppendLine("System Memory: " + (Environment.WorkingSet / (1024 * 1024)) + " MB");
            sb.AppendLine("System Uptime: " + TimeSpan.FromMilliseconds(Environment.TickCount64));
            sb.AppendLine("Current Directory: " + Environment.CurrentDirectory);
            sb.AppendLine("System Drive: " + Environment.GetFolderPath(Environment.SpecialFolder.System));
            sb.AppendLine("Temp Directory: " + Environment.GetEnvironmentVariable("TEMP"));
            sb.AppendLine("Processor Architecture: " + Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE"));
            sb.AppendLine("Processor Identifier: " + Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"));
            sb.AppendLine("Processor Level: " + Environment.GetEnvironmentVariable("PROCESSOR_LEVEL"));
            sb.AppendLine("Processor Revision: " + Environment.GetEnvironmentVariable("PROCESSOR_REVISION"));

            sb.AppendLine("System Page Size: " + Environment.SystemPageSize);
            sb.AppendLine("User Domain Name: " + Environment.UserDomainName);
            sb.AppendLine("Logical Drives: " + string.Join(", ", Environment.GetLogicalDrives()));
            sb.AppendLine("CLR Version: " + Environment.Version);
            sb.AppendLine("Command Line: " + Environment.CommandLine);
            sb.AppendLine("System Boot Time: " + DateTime.Now.Subtract(TimeSpan.FromMilliseconds(Environment.TickCount64)));
            sb.AppendLine("Number of Processors: " + Environment.ProcessorCount);
            sb.AppendLine("User Interactive: " + Environment.UserInteractive);
            sb.AppendLine("Is Debugger Attached: " + Debugger.IsAttached);
            sb.AppendLine("OS Platform: " + Environment.OSVersion.Platform);
            sb.AppendLine("OS Version String: " + Environment.OSVersion.VersionString);
            sb.AppendLine("Environment Variables:");

            sb.AppendLine("Hardware Information:");
            sb.AppendLine("  BIOS Version: " + GetWMIValue("Win32_BIOS", "Version"));
            sb.AppendLine("  BIOS Serial Number: " + GetWMIValue("Win32_BIOS", "SerialNumber"));
            sb.AppendLine("  Motherboard Manufacturer: " + GetWMIValue("Win32_BaseBoard", "Manufacturer"));
            sb.AppendLine("  Motherboard Product: " + GetWMIValue("Win32_BaseBoard", "Product"));
            sb.AppendLine("  CPU Name: " + GetWMIValue("Win32_Processor", "Name"));
            sb.AppendLine("  CPU Manufacturer: " + GetWMIValue("Win32_Processor", "Manufacturer"));
            sb.AppendLine("  CPU Current Clock Speed: " + GetWMIValue("Win32_Processor", "CurrentClockSpeed"));
            sb.AppendLine("  GPU Name: " + GetWMIValue("Win32_VideoController", "Name"));
            sb.AppendLine("  GPU Driver Version: " + GetWMIValue("Win32_VideoController", "DriverVersion"));
            sb.AppendLine("  Total Physical Memory: " + GetWMIValue("Win32_ComputerSystem", "TotalPhysicalMemory"));
            sb.AppendLine("  Available Physical Memory: " + GetWMIValue("Win32_OperatingSystem", "FreePhysicalMemory"));
            sb.AppendLine("  Total Virtual Memory: " + GetWMIValue("Win32_OperatingSystem", "TotalVirtualMemorySize"));
            sb.AppendLine("  Available Virtual Memory: " + GetWMIValue("Win32_OperatingSystem", "FreeVirtualMemory"));
            sb.AppendLine("  Disk Drives:");
            foreach (var drive in DriveInfo.GetDrives())
            {
                sb.AppendLine($"    {drive.Name} - {drive.DriveType} - {drive.TotalSize / (1024 * 1024 * 1024)} GB - {drive.AvailableFreeSpace / (1024 * 1024 * 1024)} GB free");
            }
            sb.AppendLine("----------------------------------------END SYSTEM INFORMATION----------------------------------------");
            sb.AppendLine();
            sb.AppendLine("----------------------------------------------START SLOG----------------------------------------------");

            return sb.ToString();
        }

        private static string GetWMIValue(string className, string propertyName)
        {
            try
            {
                var searcher = new System.Management.ManagementObjectSearcher($"SELECT {propertyName} FROM {className}");
                foreach (var obj in searcher.Get())
                {
                    return obj[propertyName]?.ToString();
                }
            }
            catch (Exception ex)
            {
                return "N/A (" + ex.Message + ")";
            }
            return "N/A";
        }

        public static void Write(string info, int mode = 0, string programName = null)
        {
            try
            {
                var threadId = Thread.CurrentThread.ManagedThreadId;
                var stackTrace = new StackTrace();
                var stackFrame = stackTrace.GetFrame(1); // 获取当前调用的堆栈帧
                var methodBase = stackFrame.GetMethod();
                var methodName = methodBase.Name;
                var className = methodBase.DeclaringType?.FullName; // 获取类名
                var memoryAddress = stackFrame.GetNativeOffset();

                string logMessage = $"[{DateTime.Now:F}][{threadId}][{className}.{methodName}][{memoryAddress}]: {info}";
                string markupText;

                switch (mode)
                {
                    case 0:
                        markupText = "[bold White][[INFO]][/]";
                        break;
                    case 1:
                        markupText = "[bold Yellow][[WARN]][/]";
                        break;
                    case 2:
                        markupText = "[bold Red][[ERROR]][/]";
                        break;
                    case 3:
                        markupText = $"[bold White][[BOARDCAST]][/][bold Magenta][[{programName}]][/]";
                        break;
                    default:
                        markupText = "[bold White][[INFO]][/]";
                        break;
                }

                AnsiConsole.Write(new Markup(markupText));
                Console.WriteLine(info); // 输出完整的日志消息
                File.AppendAllText(LogFilePath, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                AnsiConsole.Write(new Markup("[bold Red][[ERROR]][/] 写入日志失败: " + ex.Message));
            }
        }


        public static void WriteNotification(string title, string info, int mode)
        {
            try
            {
                var threadId = Thread.CurrentThread.ManagedThreadId;

                string logMessage = $"[{DateTime.Now:F}][{threadId}]:";
                string markupText;
                switch (mode)
                {
                    case 0:
                        markupText = $"[bold White][[Notification]][/][[{title}]]";
                        break;
                    case 1:
                        markupText = $"[bold Yellow][[Notification]][/][[{title}]]";
                        break;
                    case 2:
                        markupText = $"[bold Red][[Notification]][/][[{title}]]";
                        break;
                    default:
                        markupText = $"[bold White][[Notification]][/][[{title}]]";
                        break;
                }
                AnsiConsole.Write(new Markup(markupText));
                if (info.Contains("\n"))
                {
                    info = info.Replace("\n", ",");
                }
                Console.WriteLine(info);
                logMessage += info;

                File.AppendAllText(LogFilePath, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                AnsiConsole.Write(new Markup("[bold Red][[ERROR]][/] 写入日志失败: " + ex.Message));
            }
        }

        public static void WriteCustom(string run, string info)
        {
            try
            {
                var threadId = Thread.CurrentThread.ManagedThreadId;
                var stackTrace = new StackTrace();
                var stackFrame = stackTrace.GetFrame(2);
                var methodBase = stackFrame.GetMethod();
                var methodName = methodBase.Name;
                var memoryAddress = stackFrame.GetNativeOffset();
                string logMessage = $"[{DateTime.Now:F}][{threadId}][{methodBase}][{methodName}][{memoryAddress}]:";

                AnsiConsole.Write(new Markup($"[bold White][[INFO]][/][bold Yellow][[{run}]][/]"));
                Console.WriteLine(info);
                logMessage += info;

                File.AppendAllText(LogFilePath, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                AnsiConsole.Write(new Markup("[bold Red][[ERROR]][/] 写入日志失败: " + ex.Message));
            }
        }
    }
}
