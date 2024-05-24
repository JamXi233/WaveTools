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

using Spectre.Console;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace WaveTools.Depend
{
    internal class Logging
    {
        private static readonly string LogFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JSG-LLC", "Logs");
        private static readonly string LogFileName = $"WaveTools_Log_{DateTime.Now:yyyyMMdd_HHmmss}.log";
        private static readonly string LogFilePath = Path.Combine(LogFolderPath, LogFileName);

        static Logging()
        {
            Directory.CreateDirectory(LogFolderPath);
            File.Create(LogFilePath).Close();
        }

        public static void Write(string info, int mode = 0, string programName = null)
        {
            var threadId = Thread.CurrentThread.ManagedThreadId;
            var stackTrace = new StackTrace();
            var stackFrame = stackTrace.GetFrame(2);
            var methodBase = stackFrame.GetMethod();
            var methodName = methodBase.Name;
            var memoryAddress = stackFrame.GetNativeOffset();

            string logMessage = $"[{DateTime.Now:F}][{threadId}][{methodBase}][{methodName}][{memoryAddress}]:";
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
            Console.WriteLine(info);
            logMessage += info;

            File.AppendAllText(LogFilePath, logMessage + Environment.NewLine);
        }

        public static void WriteNotification(string title, string info, int mode)
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
            if(info.Contains("\n"))
            {
                info = info.Replace("\n", ",");
            }
            Console.WriteLine(info);
            logMessage += info;

            File.AppendAllText(LogFilePath, logMessage + Environment.NewLine);
        }

        public static void WriteCustom(string run, string info)
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
    }
}
