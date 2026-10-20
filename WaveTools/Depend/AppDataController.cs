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


using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Windows.ApplicationModel;
using Windows.Storage;
using static WaveTools.Depend.CommonHelpers;

namespace WaveTools.Depend
{
    internal class AppDataController
    {
        private const string KeyPath = "WaveTools";
        private const string FirstRun = "Config_FirstRun";
        private const string ConfigFileName = "settings.json";
        private const string BootstrapFileName = "bootstrap.json";
        private const string PortableFileName = "portable.ini";
        private const string PortableDataRootKey = "DataRootPath";
        private const string LastRunAppVersion = "Config_LastRunAppVersion";

        private static readonly object Locker = new object();
        private static Dictionary<string, JToken> settingsCache;
        private static bool isLoaded;
        private static string cachedDataRootPath;

        private static readonly string[] KnownDataDirectoryNames =
        {
            "Cache",
            "Depends",
            "Fonts",
            "GachaLinks",
            "GachaRecords",
            "GachaScreenshots",
            "Installer",
            "Panic",
            "Posts"
        };

        private static readonly HashSet<string> FirstRunDefaultKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Config_AutoCheckUpdate",
            "Config_DayNight",
            "Config_GamePath",
            "Config_UpdateService",
            "Config_FirstRun",
            "Config_FirstRunStatus",
            "Config_ConsoleMode",
            "Config_TerminalMode",
            "Config_AdminMode"
        };

        private static readonly HashSet<string> LegacyMigrationIgnoredKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Config_FirstRunStatus"
        };

        public static string DefaultDataRootPath => AppVersionHelper.IsPortable()
            ? PortableDefaultDataRootPath
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "JSG-LLC",
                "WaveTools"
            );

        private static string PortableRootPath => AppVersionHelper.GetPortableRootPath() ?? AppVersionHelper.GetPortableCurrentPath();

        private static string PortableDefaultDataRootPath => Path.Combine(PortableRootPath, "UserData");

        private static string PortableFilePath => Path.Combine(PortableRootPath, PortableFileName);

        public static string BootstrapRootPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JSG-LLC",
            "WaveTools"
        );

        public static string BootstrapFilePath => Path.Combine(BootstrapRootPath, BootstrapFileName);

        public static string DataRootPath => GetDataRootPath();

        public static string SettingsFilePath => Path.Combine(DataRootPath, ConfigFileName);

        public static bool IsDataRootReadyForLogging()
        {
            lock (Locker)
            {
                if (isLoaded)
                {
                    return true;
                }
            }

            return HasPersistedSettingsWithoutCreating();
        }

        private static bool HasPersistedSettingsWithoutCreating()
        {
            try
            {
                return File.Exists(SettingsFilePath);
            }
            catch
            {
                return false;
            }
        }

        public static string CurrentAppVersion
        {
            get
            {
                return AppVersionHelper.GetAppVersion();
            }
        }

        public static string GetDataPath(params string[] paths)
        {
            if (paths == null || paths.Length == 0)
            {
                return DataRootPath;
            }

            string result = DataRootPath;
            foreach (string path in paths)
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    result = Path.Combine(result, path);
                }
            }

            return result;
        }

        public static bool IsUsingCustomDataRoot()
        {
            if (AppVersionHelper.IsPortable())
            {
                return HasPortableDataRootOverride();
            }

            BootstrapInfo bootstrapInfo = LoadBootstrapInfo();
            return bootstrapInfo.UseCustomDataRoot && !string.IsNullOrWhiteSpace(bootstrapInfo.DataRootPath);
        }

        public static string GetBootstrapDataRootPath()
        {
            if (AppVersionHelper.IsPortable())
            {
                string portableDataRootPath = LoadPortableDataRootPath();
                return string.IsNullOrWhiteSpace(portableDataRootPath) ? DefaultDataRootPath : portableDataRootPath;
            }

            BootstrapInfo bootstrapInfo = LoadBootstrapInfo();
            return bootstrapInfo.DataRootPath;
        }

        public static bool UseDefaultDataRoot(bool copyCurrentData, out string errorMessage)
        {
            return ChangeDataRoot(DefaultDataRootPath, false, copyCurrentData, out errorMessage);
        }

        public static bool UseCustomDataRoot(string customDataRootPath, bool copyCurrentData, out string errorMessage)
        {
            return ChangeDataRoot(customDataRootPath, true, copyCurrentData, out errorMessage);
        }

        public static bool MoveToDefaultDataRoot(out string errorMessage)
        {
            return ChangeDataRootAndMove(DefaultDataRootPath, false, out errorMessage);
        }

        public static bool MoveToCustomDataRoot(string customDataRootPath, out string errorMessage)
        {
            return ChangeDataRootAndMove(customDataRootPath, true, out errorMessage);
        }

        public static bool IsCurrentDataRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                string currentPath = NormalizeDirectoryPath(DataRootPath);
                string targetPath = NormalizeDirectoryPath(path);
                return string.Equals(currentPath, targetPath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static bool HasExistingWaveToolsData(string dataRootPath, bool acceptAnySettingsFile)
        {
            if (string.IsNullOrWhiteSpace(dataRootPath))
            {
                return false;
            }

            try
            {
                string normalizedDataRootPath = Path.GetFullPath(dataRootPath.Trim());
                string settingsFilePath = Path.Combine(normalizedDataRootPath, ConfigFileName);

                if (File.Exists(settingsFilePath) && (acceptAnySettingsFile || IsExistingSettingsFile(settingsFilePath)))
                {
                    return true;
                }

                foreach (string directoryName in KnownDataDirectoryNames)
                {
                    if (Directory.Exists(Path.Combine(normalizedDataRootPath, directoryName)))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool IsExistingSettingsFile(string settingsFilePath)
        {
            try
            {
                string json = File.ReadAllText(settingsFilePath);
                Dictionary<string, JToken> data = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(json);

                if (data == null || data.Count == 0)
                {
                    return false;
                }

                if (data.TryGetValue(LastRunAppVersion, out JToken lastRunVersion) && !string.IsNullOrWhiteSpace(lastRunVersion?.ToString()))
                {
                    return true;
                }

                if (data.TryGetValue(FirstRun, out JToken firstRun) && TryReadInt(firstRun, out int firstRunValue) && firstRunValue == 0)
                {
                    return true;
                }

                foreach (string key in data.Keys)
                {
                    if (!FirstRunDefaultKeys.Contains(key))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool TryReadInt(JToken token, out int value)
        {
            value = 0;

            if (token == null || token.Type == JTokenType.Null)
            {
                return false;
            }

            try
            {
                value = token.ToObject<int>();
                return true;
            }
            catch
            {
                return int.TryParse(token.ToString(), out value);
            }
        }

        public void FirstRunInit()
        {
            SetDefaultIfNull("Config_AutoCheckUpdate", 0);
            SetDefaultIfNull("Config_GamePath", "Null");
            SetDefaultIfNull("Config_FirstRun", 1);
            SetDefaultIfNull("Config_FirstRunStatus", 0);
            SetDefaultIfNull("Config_ConsoleMode", 0);
            SetDefaultIfNull("Config_TerminalMode", 0);
            SetDefaultIfNull("Config_AdminMode", 0);
        }

        public int CheckOldData()
        {
            try
            {
                if (GetFirstRun() == 0)
                {
                    return 1;
                }

                Dictionary<string, object> legacyValues = LoadLegacyApplicationDataValues();
                if (IsLegacyApplicationDataCompleted(legacyValues))
                {
                    return 1;
                }
            }
            catch
            {
            }

            return 0;
        }

        public static void ResetCache()
        {
            lock (Locker)
            {
                settingsCache = null;
                isLoaded = false;
                cachedDataRootPath = null;
            }
        }

        public static void EnsureDataRootReady()
        {
            Directory.CreateDirectory(DataRootPath);
            EnsureLoaded();
        }

        public static void ClearAllData()
        {
            lock (Locker)
            {
                string dataRootPath = DataRootPath;

                if (Directory.Exists(dataRootPath))
                {
                    Directory.Delete(dataRootPath, true);
                }

                settingsCache = null;
                isLoaded = false;
                cachedDataRootPath = null;
            }
        }

        private static bool ChangeDataRoot(string newDataRootPath, bool useCustomDataRoot, bool copyCurrentData, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                if (!TryNormalizeAndValidateDataRoot(newDataRootPath, out string normalizedNewDataRootPath, out errorMessage))
                {
                    return false;
                }

                string oldDataRootPath = DataRootPath;

                if (copyCurrentData &&
                    Directory.Exists(oldDataRootPath) &&
                    !string.Equals(
                        NormalizeDirectoryPath(oldDataRootPath),
                        NormalizeDirectoryPath(normalizedNewDataRootPath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    CopyDirectory(oldDataRootPath, normalizedNewDataRootPath, true);
                }

                WriteDataRootLocation(useCustomDataRoot, normalizedNewDataRootPath);

                ResetCache();
                Directory.CreateDirectory(DataRootPath);
                EnsureLoaded();

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static bool ChangeDataRootAndMove(string newDataRootPath, bool useCustomDataRoot, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                if (!TryNormalizeAndValidateDataRoot(newDataRootPath, out string normalizedNewDataRootPath, out errorMessage))
                {
                    return false;
                }

                string oldDataRootPath = DataRootPath;
                string normalizedOldDataRootPath = NormalizeDirectoryPath(oldDataRootPath);
                string normalizedTargetDataRootPath = NormalizeDirectoryPath(normalizedNewDataRootPath);

                if (string.Equals(normalizedOldDataRootPath, normalizedTargetDataRootPath, StringComparison.OrdinalIgnoreCase))
                {
                    WriteDataRootLocation(useCustomDataRoot, normalizedNewDataRootPath);
                    ResetCache();
                    Directory.CreateDirectory(DataRootPath);
                    EnsureLoaded();
                    return true;
                }

                if (IsParentOrChildDirectory(normalizedOldDataRootPath, normalizedTargetDataRootPath))
                {
                    errorMessage = "新旧数据目录不能互为父子目录\n请另外选择一个独立目录";
                    return false;
                }

                EnsureLoaded();

                lock (Locker)
                {
                    SaveLocked();
                }

                if (Directory.Exists(oldDataRootPath))
                {
                    CopyDirectory(oldDataRootPath, normalizedNewDataRootPath, true);

                    if (!VerifyDirectoryCopied(oldDataRootPath, normalizedNewDataRootPath, out errorMessage))
                    {
                        return false;
                    }
                }
                else
                {
                    Directory.CreateDirectory(normalizedNewDataRootPath);
                }

                WriteDataRootLocation(useCustomDataRoot, normalizedNewDataRootPath);

                ResetCache();
                Directory.CreateDirectory(DataRootPath);
                EnsureLoaded();

                if (Directory.Exists(oldDataRootPath))
                {
                    try
                    {
                        Directory.Delete(oldDataRootPath, true);
                    }
                    catch (Exception ex)
                    {
                        errorMessage = "数据已移动并切换到到新目录\n但删除旧目录失败(可能是文件/文件夹被占用)：\n" + ex.Message;
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static bool TryNormalizeAndValidateDataRoot(string dataRootPath, out string normalizedDataRootPath, out string errorMessage)
        {
            normalizedDataRootPath = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(dataRootPath))
            {
                errorMessage = "数据目录不能为空。";
                return false;
            }

            normalizedDataRootPath = Path.GetFullPath(dataRootPath.Trim());

            if (!IsDirectoryWritable(normalizedDataRootPath, out errorMessage))
            {
                return false;
            }

            return true;
        }

        private static void WriteBootstrapInfo(bool useCustomDataRoot, string dataRootPath)
        {
            Directory.CreateDirectory(BootstrapRootPath);

            BootstrapInfo bootstrapInfo = new BootstrapInfo
            {
                UseCustomDataRoot = useCustomDataRoot,
                DataRootPath = useCustomDataRoot ? Path.GetFullPath(dataRootPath) : DefaultDataRootPath,
                UpdatedAt = DateTime.Now
            };

            File.WriteAllText(
                BootstrapFilePath,
                JsonConvert.SerializeObject(bootstrapInfo, Formatting.Indented),
                System.Text.Encoding.UTF8
            );
        }

        private static void WriteDataRootLocation(bool useCustomDataRoot, string dataRootPath)
        {
            if (AppVersionHelper.IsPortable())
            {
                if (useCustomDataRoot)
                {
                    WritePortableDataRootPath(dataRootPath);
                }
                else
                {
                    DeletePortableFile();
                }

                return;
            }

            WriteBootstrapInfo(useCustomDataRoot, dataRootPath);
        }

        private static bool HasPortableDataRootOverride()
        {
            try
            {
                return File.Exists(PortableFilePath) && !string.IsNullOrWhiteSpace(LoadPortableDataRootPath());
            }
            catch
            {
                return false;
            }
        }

        private static string LoadPortableDataRootPath()
        {
            try
            {
                if (!File.Exists(PortableFilePath))
                {
                    return null;
                }

                foreach (string rawLine in File.ReadAllLines(PortableFilePath))
                {
                    string line = rawLine?.Trim();

                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("[") || line.StartsWith(";") || line.StartsWith("#"))
                    {
                        continue;
                    }

                    int separatorIndex = line.IndexOf('=');
                    string value = separatorIndex >= 0 ? line.Substring(separatorIndex + 1).Trim() : line;

                    if (separatorIndex >= 0)
                    {
                        string key = line.Substring(0, separatorIndex).Trim();
                        if (!string.Equals(key, PortableDataRootKey, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    if (value.Length >= 2 &&
                        ((value[0] == '"' && value[value.Length - 1] == '"') ||
                         (value[0] == '\'' && value[value.Length - 1] == '\'')))
                    {
                        value = value.Substring(1, value.Length - 2);
                    }

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    return Path.IsPathRooted(value)
                        ? Path.GetFullPath(value)
                        : Path.GetFullPath(Path.Combine(PortableRootPath, value));
                }
            }
            catch
            {
            }

            return null;
        }

        private static void WritePortableDataRootPath(string dataRootPath)
        {
            Directory.CreateDirectory(PortableRootPath);

            File.WriteAllLines(
                PortableFilePath,
                new[]
                {
                    "[Portable]",
                    PortableDataRootKey + "=" + Path.GetFullPath(dataRootPath)
                },
                System.Text.Encoding.UTF8
            );
        }

        private static void DeletePortableFile()
        {
            try
            {
                if (File.Exists(PortableFilePath))
                {
                    File.Delete(PortableFilePath);
                }
            }
            catch
            {
            }
        }

        private static string GetDataRootPath()
        {
            if (!string.IsNullOrWhiteSpace(cachedDataRootPath))
            {
                return cachedDataRootPath;
            }

            if (AppVersionHelper.IsPortable())
            {
                string portableDataRootPath = LoadPortableDataRootPath();

                if (!string.IsNullOrWhiteSpace(portableDataRootPath) && IsDirectoryWritable(portableDataRootPath, out _))
                {
                    cachedDataRootPath = Path.GetFullPath(portableDataRootPath);
                    return cachedDataRootPath;
                }

                cachedDataRootPath = DefaultDataRootPath;
                return cachedDataRootPath;
            }

            BootstrapInfo bootstrapInfo = LoadBootstrapInfo();

            if (bootstrapInfo.UseCustomDataRoot && !string.IsNullOrWhiteSpace(bootstrapInfo.DataRootPath))
            {
                string customPath = bootstrapInfo.DataRootPath;
                if (IsDirectoryWritable(customPath, out _))
                {
                    cachedDataRootPath = Path.GetFullPath(customPath);
                    return cachedDataRootPath;
                }
            }

            cachedDataRootPath = DefaultDataRootPath;
            return cachedDataRootPath;
        }

        private static BootstrapInfo LoadBootstrapInfo()
        {
            try
            {
                if (!File.Exists(BootstrapFilePath))
                {
                    return new BootstrapInfo
                    {
                        UseCustomDataRoot = false,
                        DataRootPath = DefaultDataRootPath
                    };
                }

                string json = File.ReadAllText(BootstrapFilePath);
                BootstrapInfo info = JsonConvert.DeserializeObject<BootstrapInfo>(json);
                if (info == null)
                {
                    return new BootstrapInfo
                    {
                        UseCustomDataRoot = false,
                        DataRootPath = DefaultDataRootPath
                    };
                }

                return info;
            }
            catch
            {
                return new BootstrapInfo
                {
                    UseCustomDataRoot = false,
                    DataRootPath = DefaultDataRootPath
                };
            }
        }

        private static bool IsDirectoryWritable(string directoryPath, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                string fullPath = Path.GetFullPath(directoryPath);
                string windowsPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string programFilesX86Path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                if (string.Equals(Path.GetPathRoot(fullPath), fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = "不能直接选择磁盘根目录。";
                    return false;
                }

                if (StartsWithDirectory(fullPath, windowsPath) ||
                    StartsWithDirectory(fullPath, programFilesPath) ||
                    StartsWithDirectory(fullPath, programFilesX86Path))
                {
                    errorMessage = "不能选择 Windows 或 Program Files 等系统目录。";
                    return false;
                }

                Directory.CreateDirectory(fullPath);

                string testFilePath = Path.Combine(fullPath, ".wavetools_write_test_" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(testFilePath, "test");
                File.Delete(testFilePath);

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "该目录不可写：" + ex.Message;
                return false;
            }
        }

        private static bool StartsWithDirectory(string path, string parent)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(parent))
            {
                return false;
            }

            string normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string normalizedParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            return normalizedPath.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeDirectoryPath(string path)
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool IsParentOrChildDirectory(string firstPath, string secondPath)
        {
            string first = NormalizeDirectoryPath(firstPath);
            string second = NormalizeDirectoryPath(secondPath);

            return StartsWithDirectory(first, second) || StartsWithDirectory(second, first);
        }

        private static bool VerifyDirectoryCopied(string sourceDirectory, string targetDirectory, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                if (!Directory.Exists(sourceDirectory))
                {
                    return true;
                }

                foreach (string sourceFilePath in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
                {
                    string relativePath = sourceFilePath.Substring(sourceDirectory.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string targetFilePath = Path.Combine(targetDirectory, relativePath);

                    if (!File.Exists(targetFilePath))
                    {
                        errorMessage = "文件未完整移动：" + relativePath;
                        return false;
                    }

                    FileInfo sourceInfo = new FileInfo(sourceFilePath);
                    FileInfo targetInfo = new FileInfo(targetFilePath);

                    if (sourceInfo.Length != targetInfo.Length)
                    {
                        errorMessage = "文件大小校验失败：" + relativePath;
                        return false;
                    }

                    if (!FileHashEquals(sourceFilePath, targetFilePath))
                    {
                        errorMessage = "文件内容校验失败：" + relativePath;
                        return false;
                    }
                }

                foreach (string sourceSubDirectory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
                {
                    string relativePath = sourceSubDirectory.Substring(sourceDirectory.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string targetSubDirectory = Path.Combine(targetDirectory, relativePath);

                    if (!Directory.Exists(targetSubDirectory))
                    {
                        errorMessage = "文件夹未完整移动：" + relativePath;
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "移动校验失败：" + ex.Message;
                return false;
            }
        }

        private static bool FileHashEquals(string firstFilePath, string secondFilePath)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream firstStream = File.OpenRead(firstFilePath))
            using (FileStream secondStream = File.OpenRead(secondFilePath))
            {
                byte[] firstHash = sha256.ComputeHash(firstStream);
                byte[] secondHash = sha256.ComputeHash(secondStream);

                if (firstHash.Length != secondHash.Length)
                {
                    return false;
                }

                for (int i = 0; i < firstHash.Length; i++)
                {
                    if (firstHash[i] != secondHash[i])
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private static void CopyDirectory(string sourceDirectory, string targetDirectory, bool overwrite)
        {
            Directory.CreateDirectory(targetDirectory);

            foreach (string sourceFilePath in Directory.GetFiles(sourceDirectory))
            {
                string targetFilePath = Path.Combine(targetDirectory, Path.GetFileName(sourceFilePath));
                File.Copy(sourceFilePath, targetFilePath, overwrite);
            }

            foreach (string sourceSubDirectory in Directory.GetDirectories(sourceDirectory))
            {
                string targetSubDirectory = Path.Combine(targetDirectory, Path.GetFileName(sourceSubDirectory));
                CopyDirectory(sourceSubDirectory, targetSubDirectory, overwrite);
            }
        }

        private static void SetDefaultIfNull(string key, object defaultValue)
        {
            EnsureLoaded();

            lock (Locker)
            {
                if (!settingsCache.ContainsKey(key) || settingsCache[key] == null || settingsCache[key].Type == JTokenType.Null)
                {
                    settingsCache[key] = JToken.FromObject(defaultValue);
                    SaveLocked();
                }
            }
        }

        private static T GetValue<T>(string key, T defaultValue = default)
        {
            lock (Locker)
            {
                if (!isLoaded && !HasPersistedSettingsWithoutCreating() && !HasCompletedLegacyApplicationData())
                {
                    return defaultValue;
                }
            }

            EnsureLoaded();

            lock (Locker)
            {
                if (settingsCache.TryGetValue(key, out JToken value) && value != null && value.Type != JTokenType.Null)
                {
                    try
                    {
                        return value.ToObject<T>();
                    }
                    catch
                    {
                        return defaultValue;
                    }
                }
            }

            return defaultValue;
        }

        private static void SetValue<T>(string key, T value)
        {
            EnsureLoaded();

            lock (Locker)
            {
                settingsCache[key] = value == null ? JValue.CreateNull() : JToken.FromObject(value);
                SaveLocked();
            }
        }

        private static void RemoveValue(string key)
        {
            EnsureLoaded();

            lock (Locker)
            {
                settingsCache.Remove(key);
                SaveLocked();
            }
        }

        private static void EnsureLoaded()
        {
            lock (Locker)
            {
                if (isLoaded)
                {
                    return;
                }

                bool settingsFileExists = File.Exists(SettingsFilePath);

                Directory.CreateDirectory(DataRootPath);
                settingsCache = LoadFromJsonFile();

                if (!settingsFileExists && MigrateCompletedLegacyApplicationDataLocked())
                {
                    SaveLocked();
                }

                isLoaded = true;
            }
        }

        private static Dictionary<string, JToken> LoadFromJsonFile()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    return new Dictionary<string, JToken>(StringComparer.OrdinalIgnoreCase);
                }

                string json = File.ReadAllText(SettingsFilePath);
                Dictionary<string, JToken> data = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(json);
                return data ?? new Dictionary<string, JToken>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, JToken>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static bool HasCompletedLegacyApplicationData()
        {
            if (File.Exists(SettingsFilePath))
            {
                return false;
            }

            return IsLegacyApplicationDataCompleted(LoadLegacyApplicationDataValues());
        }

        private static bool MigrateCompletedLegacyApplicationDataLocked()
        {
            Dictionary<string, object> legacyValues = LoadLegacyApplicationDataValues();

            if (!IsLegacyApplicationDataCompleted(legacyValues))
            {
                return false;
            }

            bool changed = false;

            foreach (KeyValuePair<string, object> item in legacyValues)
            {
                if (string.IsNullOrWhiteSpace(item.Key) || item.Value == null || LegacyMigrationIgnoredKeys.Contains(item.Key))
                {
                    continue;
                }

                if (!settingsCache.ContainsKey(item.Key))
                {
                    settingsCache[item.Key] = JToken.FromObject(item.Value);
                    changed = true;
                }
            }

            return changed;
        }

        private static bool IsLegacyApplicationDataCompleted(Dictionary<string, object> legacyValues)
        {
            if (legacyValues == null || legacyValues.Count == 0)
            {
                return false;
            }

            if (legacyValues.TryGetValue(FirstRun, out object firstRunValue) && TryReadIntFromObject(firstRunValue, out int firstRun) && firstRun == 0)
            {
                return true;
            }

            return false;
        }

        private static Dictionary<string, object> LoadLegacyApplicationDataValues()
        {
            Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            try
            {
                ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

                foreach (KeyValuePair<string, object> item in localSettings.Values)
                {
                    if (string.IsNullOrWhiteSpace(item.Key) || item.Value == null)
                    {
                        continue;
                    }

                    if (!result.ContainsKey(item.Key))
                    {
                        result[item.Key] = item.Value;
                    }
                }

                if (localSettings.Containers.ContainsKey(KeyPath))
                {
                    ApplicationDataContainer oldContainer = localSettings.Containers[KeyPath];

                    foreach (KeyValuePair<string, object> item in oldContainer.Values)
                    {
                        if (string.IsNullOrWhiteSpace(item.Key) || item.Value == null)
                        {
                            continue;
                        }

                        if (!result.ContainsKey(item.Key))
                        {
                            result[item.Key] = item.Value;
                        }
                    }
                }
            }
            catch
            {
            }

            return result;
        }

        private static bool TryReadIntFromObject(object value, out int result)
        {
            result = 0;

            if (value == null)
            {
                return false;
            }

            try
            {
                if (value is int intValue)
                {
                    result = intValue;
                    return true;
                }

                if (value is long longValue)
                {
                    result = Convert.ToInt32(longValue);
                    return true;
                }

                if (value is double doubleValue)
                {
                    result = Convert.ToInt32(doubleValue);
                    return true;
                }

                return int.TryParse(value.ToString(), out result);
            }
            catch
            {
                return false;
            }
        }

        private static void SaveLocked()
        {
            Directory.CreateDirectory(DataRootPath);
            string json = JsonConvert.SerializeObject(settingsCache, Formatting.Indented);
            File.WriteAllText(SettingsFilePath, json, System.Text.Encoding.UTF8);
        }

        public static int GetAutoCheckUpdate() => GetValue("Config_AutoCheckUpdate", -1);
        public static int GetFirstRun() => GetValue("Config_FirstRun", -1);
        public static int GetFirstRunStatus() => GetValue("Config_FirstRunStatus", -1);
        public static string GetGamePath() => GetValue("Config_GamePath", "Null");
        public static string GetGamePathWithoutGameName() => GetGamePath().Replace("Wuthering Waves.exe", "");
        public static string GetGamePathForHelper() => "\"" + GetGamePath() + "\"";
        public static int GetUpdateService() => GetValue("Config_UpdateService", -1);
        public static int GetDayNight() => GetValue("Config_DayNight", 2);
        public static int GetConsoleMode() => GetValue("Config_ConsoleMode", -1);
        public static int GetTerminalMode() => GetValue("Config_TerminalMode", -1);
        public static int GetAccountChangeMode() => GetValue("Config_AccountChange", -1);
        public static int GetAdminMode() => GetValue("Config_AdminMode", -1);
        public static string GetGameParameter() => GetValue("Config_GameParameter", "");
        public static string GetGameResourceQuality(string gamePath)
        {
            var values = GetValue("Config_GameResourceQualities", new Dictionary<string, string>());
            var qualities = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
            return qualities.TryGetValue(GameResourceQuality.GetDirectory(gamePath), out string quality) ? quality : null;
        }

        public static void SetGameResourceQuality(string gamePath, string quality)
        {
            lock (Locker)
            {
                var values = GetValue("Config_GameResourceQualities", new Dictionary<string, string>());
                var qualities = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
                qualities[GameResourceQuality.GetDirectory(gamePath)] = quality;
                SetValue("Config_GameResourceQualities", qualities);
            }
        }
        public static int GetDX11Enable() => GetValue("Config_DX11Enable", -1);
        public static string GetLastRunAppVersion() => GetValue(LastRunAppVersion, "");
        public static bool HasThemeSetting() => HasSettingValue("Config_DayNight");
        public static bool HasUpdateServiceSetting() => HasSettingValue("Config_UpdateService");

        public static bool HasWaveToolsHelper()
        {
            try
            {
                return File.Exists(GetDataPath("Depends", "WaveToolsHelper", "WaveToolsHelper.exe"));
            }
            catch
            {
                return false;
            }
        }

        public static void SetAutoCheckUpdate(int autocheckupdate) => SetValue("Config_AutoCheckUpdate", autocheckupdate);
        public static void SetFirstRunStatus(int firstRunStatus) => SetValue("Config_FirstRunStatus", firstRunStatus);
        public static void SetFirstRun(int firstRun) => SetValue("Config_FirstRun", firstRun);
        public static void SetGamePath(string gamePath) => SetValue("Config_GamePath", gamePath);
        public static void SetUpdateService(int updateService) => SetValue("Config_UpdateService", updateService);
        public static void SetDayNight(int dayNight) => SetValue("Config_DayNight", dayNight);
        public static void SetConsoleMode(int consoleMode) => SetValue("Config_ConsoleMode", consoleMode);
        public static void SetTerminalMode(int terminalMode) => SetValue("Config_TerminalMode", terminalMode);
        public static void SetAccountChangeMode(int accountChangeMode) => SetValue("Config_AccountChange", accountChangeMode);
        public static void SetAdminMode(int adminMode) => SetValue("Config_AdminMode", adminMode);
        public static void SetGameParameter(string gamePath) => SetValue("Config_GameParameter", gamePath);
        public static void SetDX11Enable(int dx11Enable) => SetValue("Config_DX11Enable", dx11Enable);
        public static void SetLastRunAppVersion(string appVersion) => SetValue(LastRunAppVersion, appVersion);
        public static void SetCurrentAppVersionAsLastRun() => SetLastRunAppVersion(CurrentAppVersion);

        public static void SetCurrentAppVersionAsLastRunIfFirstRunCompleted()
        {
            if (GetFirstRun() == 0)
            {
                SetCurrentAppVersionAsLastRun();
            }
        }

        public static void RMAutoCheckUpdate() => RemoveValue("Config_AutoCheckUpdate");
        public static void RMFirstRunStatus() => RemoveValue("Config_FirstRunStatus");
        public static void RMFirstRun() => RemoveValue("Config_FirstRun");
        public static void RMGamePath() => RemoveValue("Config_GamePath");
        public static void RMUpdateService() => RemoveValue("Config_UpdateService");
        public static void RMDayNight() => RemoveValue("Config_DayNight");
        public static void RMConsoleMode() => RemoveValue("Config_ConsoleMode");
        public static void RMTerminalMode() => RemoveValue("Config_TerminalMode");
        public static void RMAccountChangeMode() => RemoveValue("Config_AccountChange");
        public static void RMAdminMode() => RemoveValue("Config_AdminMode");
        public static void RMGameParameter() => RemoveValue("Config_GameParameter");
        public static void RMLastRunAppVersion() => RemoveValue(LastRunAppVersion);

        private static bool HasSettingValue(string key)
        {
            EnsureLoaded();

            lock (Locker)
            {
                if (!settingsCache.TryGetValue(key, out JToken value) || value == null || value.Type == JTokenType.Null)
                {
                    return false;
                }

                return !string.IsNullOrWhiteSpace(value.ToString());
            }
        }

        private sealed class BootstrapInfo
        {
            [JsonProperty("useCustomDataRoot")]
            public bool UseCustomDataRoot { get; set; }

            [JsonProperty("dataRootPath")]
            public string DataRootPath { get; set; }

            [JsonProperty("updatedAt")]
            public DateTime UpdatedAt { get; set; }
        }
    }
}
