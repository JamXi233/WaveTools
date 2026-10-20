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

using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.WindowsAPICodePack.Taskbar;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;
using static WaveTools.App;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.Text.RegularExpressions;
using System.Net.Http;
using Newtonsoft.Json.Linq;

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

            public async static Task<string> OpenFile(string filter)
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

            private static string OpenFileWithWin32Dialog(string filter)
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

            public async static Task<string> SaveFile(string suggestFileName, Dictionary<string, List<string>> fileTypeChoices, string defaultExtension)
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

            private static string SaveFileWithWin32Dialog(string suggestFileName, Dictionary<string, List<string>> fileTypeChoices, string defaultExtension)
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

        public static class WebViewHelper
        {
            private const int GWL_STYLE = -16;
            private const uint WS_SYSMENU = 0x80000;
            private const uint WS_MAXIMIZEBOX = 0x10000;
            private const uint WS_MINIMIZEBOX = 0x20000;

            [DllImport("user32.dll", SetLastError = true)]
            private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

            public static async void RaiseWebViewWindow(string url, string title, bool inwindow = false, int width = 1141, int height = 641, string script = null)
            {
                WaitOverlayManager.RaiseWaitOverlay(true, "正在检查链接", "请耐心等待", true, 0);
                if (!url.Contains("https"))
                {
                    NotificationManager.RaiseNotification("打开失败", "链接不正确", InfoBarSeverity.Warning);
                    WaitOverlayManager.RaiseWaitOverlay(false); // 添加这行以确保在错误情况下取消等待覆盖
                    return;
                }

                var newWindow = new Window();
                newWindow.Title = $"{title}";
                newWindow.SetTitleBar(null);
                newWindow.ExtendsContentIntoTitleBar = true;

                WaitOverlayManager.RaiseWaitOverlay(true, "页面已在新窗口打开", null, false, 0);

                var webView = new WebView2
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                };

                var grid = new Grid();
                grid.Children.Add(webView);

                newWindow.Content = grid;

                IntPtr hWnd = WindowNative.GetWindowHandle(newWindow);
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
                AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

                // Remove minimize, maximize, and close buttons
                uint styles = GetWindowLong(hWnd, GWL_STYLE);
                styles &= ~(WS_SYSMENU | WS_MAXIMIZEBOX | WS_MINIMIZEBOX);
                SetWindowLong(hWnd, GWL_STYLE, styles);

                appWindow.Resize(new SizeInt32(width, height));
                newWindow.Closed += (s, args) =>
                {
                    WaitOverlayManager.RaiseWaitOverlay(false);
                };

                newWindow.Activate();
                await webView.EnsureCoreWebView2Async(null);

                // 注入脚本
                if (!string.IsNullOrEmpty(script))
                {
                    webView.CoreWebView2.DOMContentLoaded += async (s, e) =>
                    {
                        await webView.CoreWebView2.ExecuteScriptAsync(script);
                    };
                }


                webView.CoreWebView2.WebMessageReceived += (sender, args) =>
                {
                    if (args.TryGetWebMessageAsString() == "announcements_link_clicked")
                    {
                        DialogManager.RaiseDialog(newWindow.Content.XamlRoot, "公告内链接", "工具箱内公告仅供查看\n点击公告内链接基本无效\n跳转的页面均需要在游戏内打开");
                    }
                };


                webView.CoreWebView2.WebMessageReceived += (sender, args) =>
                {
                    if (args.TryGetWebMessageAsString() == "window_closed")
                    {
                        newWindow.Close();
                    }
                };

                webView.Source = new Uri(url);
            }

        }


        public sealed class PortableUpdateInfo
        {
            public PortableUpdateInfo(int status, string version, string changelog, string downloadUrl)
            {
                Status = status;
                Version = version ?? string.Empty;
                Changelog = changelog ?? string.Empty;
                DownloadUrl = downloadUrl ?? string.Empty;
            }

            public int Status { get; }

            public string Version { get; }

            public string Changelog { get; }

            public string DownloadUrl { get; }
        }

        public static class AppVersionHelper
        {
            public static string GetAppVersion()
            {
                if (TryGetPackageVersion(out string packageVersion))
                {
                    return packageVersion;
                }

                string manifestVersion = GetManifestVersion();

                if (!string.IsNullOrWhiteSpace(manifestVersion))
                {
                    return manifestVersion;
                }

                string fileVersion = GetFileVersion();

                if (!string.IsNullOrWhiteSpace(fileVersion))
                {
                    return fileVersion;
                }

                Version assemblyVersion = Assembly.GetEntryAssembly()?.GetName().Version;

                if (assemblyVersion != null)
                {
                    return assemblyVersion.ToString();
                }

                return "0.0.0.0";
            }

            public static string GetAppName()
            {
                try
                {
                    string displayName = Package.Current.DisplayName;

                    if (!string.IsNullOrWhiteSpace(displayName))
                    {
                        return displayName;
                    }
                }
                catch
                {
                }

                string manifestName = GetManifestName();

                if (!string.IsNullOrWhiteSpace(manifestName))
                {
                    return manifestName;
                }

                try
                {
                    string filePath = Process.GetCurrentProcess().MainModule?.FileName;

                    if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                    {
                        FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(filePath);

                        if (!string.IsNullOrWhiteSpace(versionInfo.ProductName))
                        {
                            return versionInfo.ProductName;
                        }
                    }
                }
                catch
                {
                }

                string assemblyName = Assembly.GetEntryAssembly()?.GetName().Name;

                if (!string.IsNullOrWhiteSpace(assemblyName))
                {
                    return assemblyName;
                }

                return "WaveTools";
            }

            public static bool IsPackaged()
            {
                try
                {
                    _ = Package.Current.Id.FullName;
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            public static bool IsPortable()
            {
                if (IsPackaged())
                {
                    return false;
                }

                string currentPath = GetPortableCurrentPath();
                DirectoryInfo currentDirectory = new DirectoryInfo(currentPath);

                if (IsVersionFolderName(currentDirectory.Name) && File.Exists(Path.Combine(currentPath, "WaveTools.exe")))
                {
                    return true;
                }

                return false;
            }

            public static string GetPortableCurrentPath()
            {
                return Path.GetFullPath(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }

            public static string GetPortableRootPath()
            {
                if (IsPackaged())
                {
                    return null;
                }

                string currentPath = GetPortableCurrentPath();
                DirectoryInfo currentDirectory = new DirectoryInfo(currentPath);
                List<string> candidates = new List<string>();

                AddSearchRoot(candidates, currentPath);
                AddSearchRoot(candidates, currentDirectory.Parent?.FullName);
                AddSearchRoot(candidates, currentDirectory.Parent?.Parent?.FullName);

                if (IsVersionFolderName(currentDirectory.Name) && currentDirectory.Parent != null)
                {
                    return currentDirectory.Parent.FullName;
                }

                foreach (string candidate in candidates)
                {
                    if (string.IsNullOrWhiteSpace(candidate))
                    {
                        continue;
                    }

                    if (File.Exists(Path.Combine(candidate, "WaveTools.Updater.exe")))
                    {
                        return candidate;
                    }
                }

                return currentDirectory.Parent?.FullName;
            }

            public static string GetPortableUpdaterPath()
            {
                string rootPath = GetPortableRootPath();
                string currentPath = GetPortableCurrentPath();
                List<string> candidates = new List<string>();

                string cachedPath = GetPortableUpdaterCachePath();

                if (!string.IsNullOrWhiteSpace(cachedPath))
                {
                    candidates.Add(cachedPath);
                }

                if (!string.IsNullOrWhiteSpace(rootPath))
                {
                    candidates.Add(Path.Combine(rootPath, "WaveTools.Updater.exe"));
                }

                candidates.Add(Path.Combine(currentPath, "WaveTools.Updater.exe"));

                foreach (string candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                return null;
            }

            public static string GetPortableUpdaterCachePath()
            {
                try
                {
                    return AppDataController.GetDataPath("Updater", "WaveTools.Updater.exe");
                }
                catch
                {
                    string fallbackRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JSG-LLC", "WaveTools", "Updater");
                    return Path.Combine(fallbackRoot, "WaveTools.Updater.exe");
                }
            }

            private static bool IsVersionFolderName(string name)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return false;
                }

                return Regex.IsMatch(name, @"^\d+(?:\.\d+){1,3}$");
            }

            private static bool TryGetPackageVersion(out string version)
            {
                version = null;

                try
                {
                    PackageVersion packageVersion = Package.Current.Id.Version;
                    version = $"{packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}.{packageVersion.Revision}";
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private static string GetManifestVersion()
            {
                try
                {
                    XElement identity = GetManifestIdentity();
                    return identity?.Attribute("Version")?.Value;
                }
                catch
                {
                    return null;
                }
            }

            private static string GetManifestName()
            {
                try
                {
                    XElement identity = GetManifestIdentity();
                    string name = identity?.Attribute("Name")?.Value;

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        return name;
                    }

                    return null;
                }
                catch
                {
                    return null;
                }
            }

            private static XElement GetManifestIdentity()
            {
                string manifestPath = FindManifestPath();

                if (string.IsNullOrWhiteSpace(manifestPath))
                {
                    return null;
                }

                XDocument document = XDocument.Load(manifestPath);
                return document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Identity");
            }

            private static string FindManifestPath()
            {
                string baseDirectory = AppContext.BaseDirectory;
                string currentDirectory = Directory.GetCurrentDirectory();
                List<string> roots = new List<string>();

                AddSearchRoot(roots, baseDirectory);
                AddSearchRoot(roots, currentDirectory);

                foreach (string root in roots.ToArray())
                {
                    string current = root;

                    for (int i = 0; i < 8 && !string.IsNullOrWhiteSpace(current); i++)
                    {
                        AddSearchRoot(roots, current);
                        DirectoryInfo parent = Directory.GetParent(current);
                        current = parent?.FullName;
                    }
                }

                foreach (string root in roots)
                {
                    string appxManifestPath = Path.Combine(root, "AppxManifest.xml");

                    if (File.Exists(appxManifestPath))
                    {
                        return appxManifestPath;
                    }

                    string packageManifestPath = Path.Combine(root, "Package.appxmanifest");

                    if (File.Exists(packageManifestPath))
                    {
                        return packageManifestPath;
                    }
                }

                return null;
            }

            private static void AddSearchRoot(List<string> roots, string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return;
                }

                try
                {
                    string fullPath = Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

                    if (!roots.Any(item => string.Equals(item, fullPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        roots.Add(fullPath);
                    }
                }
                catch
                {
                }
            }

            private static string GetFileVersion()
            {
                try
                {
                    string filePath = Process.GetCurrentProcess().MainModule?.FileName;

                    if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    {
                        return null;
                    }

                    FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(filePath);

                    if (!string.IsNullOrWhiteSpace(versionInfo.FileVersion))
                    {
                        return versionInfo.FileVersion;
                    }

                    if (!string.IsNullOrWhiteSpace(versionInfo.ProductVersion))
                    {
                        return versionInfo.ProductVersion;
                    }

                    return null;
                }
                catch
                {
                    return null;
                }
            }
        }

        public static class PortableUpdateHelper
        {
            private const string JsgPortablePackage = "cn.jamsg.wavetools.portable";
            private const string JsgPortableUpdaterDownloadUrl = "https://api.jamsg.cn/release/getversion?package=cn.jamsg.wavetools.portable.updater&mode=download";
            private const string GithubOwner = "JamXi233";
            private const string GithubRepo = "WaveTools";
            private static readonly HttpClient HttpClient = CreateHttpClient();

            public static async Task<PortableUpdateInfo> GetPortableUpdateAsync(bool force = false)
            {
                try
                {
                    PortableUpdateInfo latestInfo = await GetLatestPortableReleaseInfoAsync();
                    string currentVersion = AppVersionHelper.GetAppVersion();

                    if (!Version.TryParse(currentVersion, out Version currentVersionParsed))
                    {
                        currentVersionParsed = new Version("0.0.0.0");
                    }

                    if (!Version.TryParse(latestInfo.Version, out Version latestVersionParsed))
                    {
                        return new PortableUpdateInfo(2, string.Empty, string.Empty, string.Empty);
                    }

                    if (force || latestVersionParsed > currentVersionParsed)
                    {
                        return new PortableUpdateInfo(1, latestInfo.Version, latestInfo.Changelog, latestInfo.DownloadUrl);
                    }

                    return new PortableUpdateInfo(0, currentVersion, string.Empty, latestInfo.DownloadUrl);
                }
                catch
                {
                    return new PortableUpdateInfo(2, string.Empty, string.Empty, string.Empty);
                }
            }

            public static async Task<PortableUpdateInfo> GetLatestPortableReleaseInfoAsync()
            {
                int updateService = AppDataController.GetUpdateService();

                if (updateService == 0)
                {
                    return await GetLatestPortableReleaseInfoFromGithubAsync();
                }

                return await GetLatestPortableReleaseInfoFromJsgAsync();
            }

            public static string BuildUpdaterArguments(PortableUpdateInfo updateInfo, string dataRootPath, bool force)
            {
                string rootPath = AppVersionHelper.GetPortableRootPath();
                string currentPath = AppVersionHelper.GetPortableCurrentPath();
                string channel = AppDataController.GetUpdateService() == 0 ? "github" : "ds";
                int processId = Process.GetCurrentProcess().Id;
                List<string> args = new List<string>
                {
                    "/pid", processId.ToString(),
                    "/root", rootPath,
                    "/current", currentPath,
                    "/dataRoot", dataRootPath,
                    "/channel", channel,
                    "/version", updateInfo?.Version ?? string.Empty,
                    "/url", updateInfo?.DownloadUrl ?? string.Empty,
                    "/launch"
                };

                if (force)
                {
                    args.Add("/force");
                }

                return BuildArguments(args.ToArray());
            }

            public static async Task<string> GetPortableUpdaterAsync(bool forceDownload = false)
            {
                string updaterPath = AppVersionHelper.GetPortableUpdaterPath();

                if (!forceDownload && !string.IsNullOrWhiteSpace(updaterPath) && File.Exists(updaterPath))
                {
                    return updaterPath;
                }

                return await DownloadPortableUpdaterAsync();
            }

            public static async Task<string> DownloadPortableUpdaterAsync()
            {
                string updaterPath = AppVersionHelper.GetPortableUpdaterCachePath();
                string updaterDirectory = Path.GetDirectoryName(updaterPath);

                if (string.IsNullOrWhiteSpace(updaterDirectory))
                {
                    throw new InvalidOperationException("无法确定便携版更新器保存目录");
                }

                Directory.CreateDirectory(updaterDirectory);

                string tempPath = updaterPath + ".download";

                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                using (HttpResponseMessage response = await HttpClient.GetAsync(JsgPortableUpdaterDownloadUrl))
                {
                    response.EnsureSuccessStatusCode();

                    using (Stream inputStream = await response.Content.ReadAsStreamAsync())
                    using (FileStream outputStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await inputStream.CopyToAsync(outputStream);
                    }
                }

                FileInfo tempFileInfo = new FileInfo(tempPath);

                if (!tempFileInfo.Exists || tempFileInfo.Length <= 0)
                {
                    throw new InvalidOperationException("下载到的WaveTools.Updater.exe为空");
                }

                if (File.Exists(updaterPath))
                {
                    File.Delete(updaterPath);
                }

                File.Move(tempPath, updaterPath);
                return updaterPath;
            }

            private static HttpClient CreateHttpClient()
            {
                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(10);
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "WaveTools-Portable-Update-Client");
                return client;
            }

            private static async Task<PortableUpdateInfo> GetLatestPortableReleaseInfoFromJsgAsync()
            {
                string[] urls =
                {
                    "https://api.jamsg.cn/release/getversion.php?package=" + JsgPortablePackage,
                    "https://api.jamsg.cn/release/getversion?package=" + JsgPortablePackage
                };

                Exception lastException = null;

                foreach (string url in urls)
                {
                    try
                    {
                        string json = await HttpClient.GetStringAsync(url);
                        JObject jsonObject = JObject.Parse(Regex.Unescape(json));
                        string version = jsonObject["version"]?.ToString();
                        string changelog = jsonObject["changelog"]?.ToString() ?? jsonObject["Changelog"]?.ToString() ?? string.Empty;
                        string downloadUrl = jsonObject["link"]?.ToString() ?? jsonObject["downloadUrl"]?.ToString() ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(downloadUrl))
                        {
                            throw new InvalidOperationException("便携版更新信息缺少 version 或 link 字段");
                        }

                        return new PortableUpdateInfo(1, NormalizeVersion(version), changelog, downloadUrl);
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                    }
                }

                throw lastException ?? new InvalidOperationException("无法获取便携版更新信息");
            }

            private static async Task<PortableUpdateInfo> GetLatestPortableReleaseInfoFromGithubAsync()
            {
                string apiUrl = $"https://api.github.com/repos/{GithubOwner}/{GithubRepo}/releases/latest";
                string json = await HttpClient.GetStringAsync(apiUrl);
                JObject jsonObject = JObject.Parse(json);
                string tagVersion = NormalizeVersion(jsonObject["tag_name"]?.ToString());
                string changelog = jsonObject["body"]?.ToString() ?? string.Empty;
                JArray assets = jsonObject["assets"] as JArray;

                if (assets == null)
                {
                    throw new InvalidOperationException("GitHub Release 中没有资源文件");
                }

                foreach (JToken asset in assets)
                {
                    string assetName = asset["name"]?.ToString() ?? string.Empty;
                    string downloadUrl = asset["browser_download_url"]?.ToString() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(downloadUrl))
                    {
                        continue;
                    }

                    Match match = Regex.Match(assetName, @"^WaveTools_(?<version>\d+(?:\.\d+){1,3})_Portable\.zip$", RegexOptions.IgnoreCase);

                    if (!match.Success)
                    {
                        continue;
                    }

                    string version = NormalizeVersion(match.Groups["version"].Value);

                    if (string.IsNullOrWhiteSpace(version))
                    {
                        version = tagVersion;
                    }

                    return new PortableUpdateInfo(1, version, changelog, downloadUrl);
                }

                foreach (JToken asset in assets)
                {
                    string assetName = asset["name"]?.ToString() ?? string.Empty;
                    string downloadUrl = asset["browser_download_url"]?.ToString() ?? string.Empty;

                    if (assetName.EndsWith("_Portable.zip", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(downloadUrl))
                    {
                        return new PortableUpdateInfo(1, tagVersion, changelog, downloadUrl);
                    }
                }

                throw new InvalidOperationException("未找到 WaveTools_X.X.X.X_Portable.zip");
            }

            private static string NormalizeVersion(string version)
            {
                if (string.IsNullOrWhiteSpace(version))
                {
                    return string.Empty;
                }

                string value = version.Trim();
                value = Regex.Replace(value, "^WaveTools[_-]", string.Empty, RegexOptions.IgnoreCase);
                value = Regex.Replace(value, "^v", string.Empty, RegexOptions.IgnoreCase);
                value = value.Replace("_Portable", string.Empty).Replace("-Portable", string.Empty).Trim('_', '-', ' ');
                Match match = Regex.Match(value, @"\d+(?:\.\d+){1,3}");
                return match.Success ? match.Value : value;
            }

            private static string BuildArguments(string[] args)
            {
                return string.Join(" ", args.Select(QuoteArgument));
            }

            private static string QuoteArgument(string value)
            {
                if (value == null)
                {
                    return "\"\"";
                }

                if (value.Length == 0)
                {
                    return "\"\"";
                }

                if (value.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '"' }) < 0)
                {
                    return value;
                }

                return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            }
        }

        public class MemHelper
        {
            public static void Release()
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

    }
}
