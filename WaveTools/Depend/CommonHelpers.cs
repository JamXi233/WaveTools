using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.WindowsAPICodePack.Taskbar;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;
using static WaveTools.App;

namespace WaveTools.Depend
{
    internal class CommonHelpers
    {
        public static class FileHelpers
        {
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

            public async static Task<string?> OpenFile(string filter)
            {
                try
                {
                    var picker = new FileOpenPicker();
                    picker.FileTypeFilter.Add(filter);
                    var hwnd = GetActiveWindow();
                    InitializeWithWindow.Initialize(picker, hwnd);
                    var file = await picker.PickSingleFileAsync();
                    return file?.Path;
                }
                catch (COMException)
                {
                    return await Task.Run(() => OpenFileWithWin32Dialog(filter)).ConfigureAwait(false);
                }
            }

            private static string? OpenFileWithWin32Dialog(string filter)
            {
                var openFileName = new OPENFILENAME
                {
                    lStructSize = Marshal.SizeOf<OPENFILENAME>(),
                    lpstrFilter = filter.Replace('|', '\0') + '\0' + '\0',
                    lpstrFile = new string(new char[256]),
                    nMaxFile = 256,
                    lpstrTitle = "选择文件",
                    Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_HIDEREADONLY
                };

                return GetOpenFileName(ref openFileName) ? openFileName.lpstrFile : null;
            }

            public async static Task<string?> SaveFile(string suggestFileName, Dictionary<string, List<string>> fileTypeChoices, string defaultExtension)
            {
                try
                {
                    var picker = new FileSavePicker
                    {
                        SuggestedFileName = suggestFileName,
                        DefaultFileExtension = defaultExtension
                    };
                    foreach (var choice in fileTypeChoices)
                    {
                        picker.FileTypeChoices.Add(choice.Key, choice.Value);
                    }

                    var hwnd = GetActiveWindow();
                    InitializeWithWindow.Initialize(picker, hwnd);
                    var file = await picker.PickSaveFileAsync();
                    return file?.Path;
                }
                catch (COMException)
                {
                    return await Task.Run(() => SaveFileWithWin32Dialog(suggestFileName, fileTypeChoices, defaultExtension)).ConfigureAwait(false);
                }
            }

            private static string? SaveFileWithWin32Dialog(string suggestFileName, Dictionary<string, List<string>> fileTypeChoices, string defaultExtension)
            {
                var saveFileName = new OPENFILENAME
                {
                    lStructSize = Marshal.SizeOf<OPENFILENAME>(),
                    lpstrFilter = CreateFilterString(fileTypeChoices),
                    lpstrFile = new string(new char[256]),
                    nMaxFile = 256,
                    lpstrTitle = "保存文件",
                    lpstrDefExt = defaultExtension.TrimStart('.'),
                    Flags = OFN_OVERWRITEPROMPT | OFN_HIDEREADONLY
                };

                if (!string.IsNullOrEmpty(suggestFileName))
                {
                    saveFileName.lpstrFile = suggestFileName;
                }

                return GetSaveFileName(ref saveFileName) ? saveFileName.lpstrFile : null;
            }

            private static string CreateFilterString(Dictionary<string, List<string>> fileTypeChoices)
            {
                var filters = new List<string>();
                foreach (var choice in fileTypeChoices)
                {
                    string filter = $"{choice.Key}\0{string.Join(";", choice.Value)}\0";
                    filters.Add(filter);
                }
                return string.Join(string.Empty, filters) + '\0';
            }

            [DllImport("comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern bool GetOpenFileName(ref OPENFILENAME lpofn);

            [DllImport("comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern bool GetSaveFileName(ref OPENFILENAME lpofn);

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
            private struct OPENFILENAME
            {
                public int lStructSize;
                public IntPtr hwndOwner;
                public IntPtr hInstance;
                public string lpstrFilter;
                public string lpstrCustomFilter;
                public int nMaxCustFilter;
                public int nFilterIndex;
                public string lpstrFile;
                public int nMaxFile;
                public string lpstrFileTitle;
                public int nMaxFileTitle;
                public string lpstrInitialDir;
                public string lpstrTitle;
                public int Flags;
                public short nFileOffset;
                public short nFileExtension;
                public string lpstrDefExt;
                public IntPtr lCustData;
                public IntPtr lpfnHook;
                public string lpTemplateName;
                public IntPtr pvReserved;
                public int dwReserved;
                public int FlagsEx;
            }

            private const int OFN_FILEMUSTEXIST = 0x00001000;
            private const int OFN_PATHMUSTEXIST = 0x00000800;
            private const int OFN_HIDEREADONLY = 0x00000004;
            private const int OFN_OVERWRITEPROMPT = 0x00000002;

            [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr GetActiveWindow();
        }


        public static class TaskbarHelper
        {
            public static void SetProgressValue(int value, int max)
            {
                if (TaskbarManager.IsPlatformSupported)
                {
                    TaskbarManager.Instance.SetProgressValue(value, max);
                }
            }

            public static void SetProgressState(TaskbarProgressBarState state)
            {
                if (TaskbarManager.IsPlatformSupported)
                {
                    TaskbarManager.Instance.SetProgressState(state);
                }
            }
        }
    }
}