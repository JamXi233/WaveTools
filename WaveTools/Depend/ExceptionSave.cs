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
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WaveTools.Depend
{
    public class ExceptionSave
    {
        private static readonly SemaphoreSlim WriteLock = new SemaphoreSlim(1, 1);

        public static async Task Write(string message, int severity, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = string.Format("WaveTools_Panic_{0:yyyyMMdd_HHmmss_fff}.WaveToolsPanic", DateTime.Now);
            }

            string folderPath = AppDataController.GetDataPath("Panic");
            Directory.CreateDirectory(folderPath);

            string safeFileName = GetSafeFileName(fileName);
            string filePath = Path.Combine(folderPath, safeFileName);
            string content = string.Format("{0} [{1}] {2}{3}", DateTime.Now, severity, message, Environment.NewLine);

            await WriteLock.WaitAsync();

            try
            {
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        await WriteToFile(filePath, content, FileMode.Append);
                        return;
                    }
                    catch (IOException)
                    {
                        await Task.Delay(50 * (i + 1));
                    }
                }

                string fallbackFilePath = GetFallbackFilePath(folderPath, safeFileName);
                await WriteToFile(fallbackFilePath, content, FileMode.Create);
            }
            finally
            {
                WriteLock.Release();
            }
        }

        private static async Task WriteToFile(string filePath, string content, FileMode fileMode)
        {
            using (FileStream stream = new FileStream(filePath, fileMode, FileAccess.Write, FileShare.ReadWrite, 4096, true))
            using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
            {
                await writer.WriteAsync(content);
            }
        }

        private static string GetFallbackFilePath(string folderPath, string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);

            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".WaveToolsPanic";
            }

            return Path.Combine(folderPath, name + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + extension);
        }

        private static string GetSafeFileName(string fileName)
        {
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            return fileName;
        }
    }
}
