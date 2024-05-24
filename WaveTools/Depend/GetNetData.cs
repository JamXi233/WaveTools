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
using System.Net.Http;
using System.Threading.Tasks;

namespace WaveTools.Depend
{
    class GetNetData
    {
        public async Task<bool> DownloadFileWithProgressAsync(string fileUrl, string localFilePath, IProgress<double> progress)
        {
            try
            {
                string directoryPath = Path.GetDirectoryName(localFilePath);
                Directory.CreateDirectory(directoryPath);
                using (var httpClient = new HttpClient())
                {
                    using (var response = await httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();

                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            long totalBytes = response.Content.Headers.ContentLength.GetValueOrDefault();

                            byte[] buffer = new byte[8192];
                            int bytesRead;
                            long bytesDownloaded = 0;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);

                                bytesDownloaded += bytesRead;
                                double progressPercentage = (double)bytesDownloaded / totalBytes * 100;
                                    progress.Report(progressPercentage);
                                
                            }
                        }
                    }
                }
                return true; // 下载成功
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return false; // 下载失败
            }
        }
    }
}
