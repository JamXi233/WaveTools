using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using static WaveTools.App;

namespace WaveTools.Depend
{
    internal class CommonHelpers
    {
        public static class FileHelpers {
            public static void OpenFileLocation(string filepath)
            {
                if (File.Exists(filepath))
                {
                    Process.Start("explorer.exe", $"/select,\"{filepath}\"");
                }
                else
                {
                    NotificationManager.RaiseNotification(filepath, "未找到文件", InfoBarSeverity.Error);
                }
            }

            public async static Task<string> OpenFile(string filter)
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add(filter);
                var window = new Window();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                try
                {
                    var file = await picker.PickSingleFileAsync();
                    if (file != null)
                    {
                        return file.Path;
                    }
                    else
                    {
                        return null;
                    }
                }
                finally
                {
                    window.Close();
                }
            }

            public async static Task<string> SaveFile(string suggestFileName, Dictionary<string, List<string>> fileTypeChoices, string defaultExtension)
            {
                var picker = new FileSavePicker();
                picker.SuggestedFileName = suggestFileName;

                // 添加文件类型选项
                foreach (var choice in fileTypeChoices)
                {
                    picker.FileTypeChoices.Add(choice.Key, choice.Value);
                }

                // 设置默认扩展名
                picker.DefaultFileExtension = defaultExtension;

                var window = new Window();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                try
                {
                    var file = await picker.PickSaveFileAsync();
                    if (file != null)
                    {
                        return file.Path;
                    }
                    else
                    {
                        return null;
                    }
                }
                finally
                {
                    window.Close();
                }
            }
        }

    }
}