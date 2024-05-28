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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace WaveTools.Depend
{
    public class ExceptionSave
    {
        public static async Task Write(string message, int severity, string fileName)
        {
            // 获取用户文档目录下的JSG-LLC\Panic目录
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JSG-LLC", "Panic");

            // 确保目录存在
            Directory.CreateDirectory(folderPath);

            // 创建文件路径
            string filePath = Path.Combine(folderPath, fileName);

            // 使用StreamWriter异步写入数据
            using (StreamWriter writer = new StreamWriter(filePath, false)) // false表示覆盖文件
            {
                await writer.WriteLineAsync($"{DateTime.Now} [{severity}] {message}");
            }
        }
    }
}
