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
using WaveTools.Depend;
using Windows.Storage;

namespace WaveTools.Depend
{
    class AppDataController
    {
        private const string KeyPath = "WaveTools";
        private const string FirstRun = "Config_FirstRun";

        public void FirstRunInit()
        {
            SetDefaultIfNull("Config_AutoCheckUpdate", 0);
            SetDefaultIfNull("Config_DayNight", 2);
            SetDefaultIfNull("Config_GamePath", "Null");
            SetDefaultIfNull("Config_UpdateService", 2);
            SetDefaultIfNull("Config_FirstRun", 1);
            SetDefaultIfNull("Config_FirstRunStatus", 0);
            SetDefaultIfNull("Config_ConsoleMode", 0);
            SetDefaultIfNull("Config_TerminalMode", 0);
            SetDefaultIfNull("Config_AdminMode", 0);
        }

        public int CheckOldData()
        {
            var keyContainer = GetOrCreateContainer(KeyPath);
            Logging.WriteCustom("AppDataController", "Checking OldData...");
            if (keyContainer.Values.ContainsKey(FirstRun) && keyContainer.Values[FirstRun].ToString() == "0")
            {
                Logging.WriteCustom("AppDataController", "OldData Found");
                return 1;
            }
            Logging.WriteCustom("AppDataController", "OldData Not Found");
            return 0;
        }

        private ApplicationDataContainer GetOrCreateContainer(string keyPath)
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            if (!localSettings.Containers.ContainsKey(keyPath))
            {
                return localSettings.CreateContainer(keyPath, ApplicationDataCreateDisposition.Always);
            }
            return localSettings.Containers[keyPath];
        }

        private void SetDefaultIfNull(string key, object defaultValue)
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values[key] == null)
            {
                localSettings.Values[key] = defaultValue;
                Logging.WriteCustom("AppDataController", $"Init {key}");
            }
        }

        private static T GetValue<T>(string key, T defaultValue = default)
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            return localSettings.Values.ContainsKey(key) ? (T)localSettings.Values[key] : defaultValue;
        }

        private static void SetValue<T>(string key, T value)
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values[key] = value;
            Logging.WriteCustom("AppDataController", $"Set {key}");
        }

        private static void RemoveValue(string key)
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values.Remove(key);
            Logging.WriteCustom("AppDataController", $"Remove {key}");
        }

        // 通用设置
        public static int GetAutoCheckUpdate() => GetValue("Config_AutoCheckUpdate", -1);
        public static int GetFirstRun() => GetValue("Config_FirstRun", -1);
        public static int GetFirstRunStatus() => GetValue("Config_FirstRunStatus", -1);
        public static string GetGamePath() => GetValue("Config_GamePath", "Null");
        public static string GetGamePathWithoutGameName() => GetGamePath().Replace("Wuthering Waves.exe", "");
        public static string GetGamePathForHelper() => "\"" + (string)ApplicationData.Current.LocalSettings.Values["Config_GamePath"] + "\"";
        public static int GetUpdateService() => GetValue("Config_UpdateService", -1);
        public static int GetDayNight() => GetValue("Config_DayNight", -1);
        public static int GetConsoleMode() => GetValue("Config_ConsoleMode", -1);
        public static int GetTerminalMode() => GetValue("Config_TerminalMode", -1);
        public static int GetAccountChangeMode() => GetValue("Config_AccountChange", -1);
        public static int GetAdminMode() => GetValue("Config_AdminMode", -1);
        public static string GetGameParameter() => GetValue("Config_GameParameter", "");

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
    }
}
