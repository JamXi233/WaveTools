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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WaveTools.Depend;
using static WaveTools.App;

namespace WaveTools.Views.SGViews
{
    public sealed partial class GraphicSettingView : Page
    {
        private static readonly (string Value, string Text)[] SwitchOptions =
            { ("0", "关闭"), ("1", "开启") };
        private static readonly (string Value, string Text)[] QualityOptions =
            { ("0", "低"), ("1", "中"), ("2", "高") };
        private static readonly (string Value, string Text)[] EffectOptions =
            { ("0", "关闭"), ("1", "低"), ("2", "中"), ("3", "高") };
        private static readonly (string Value, string Text)[] SuperResolutionOptions =
            { ("99", "自动"), ("-2", "超级性能"), ("-1", "性能"), ("0", "平衡"), ("1", "质量"), ("3", "DLAA") };
        private static readonly (string Value, string Text)[] LegacySuperResolutionOptions =
            { ("0", "关闭"), ("1", "自动"), ("3", "质量"), ("4", "平衡"), ("5", "性能"), ("6", "超级性能") };
        private static readonly Lazy<Task<int>> FrameGenerationLimit =
            new(() => Task.Run(DetectFrameGenerationLimit));

        private sealed class SettingBinding
        {
            public string Key;
            public DropDownButton Button;
            public (string Value, string Text)[] Options;
        }

        private sealed class SettingChoice
        {
            public SettingBinding Setting;
            public string Key;
            public string Value;
        }

        private readonly List<SettingBinding> settings = new();
        private readonly string gamePath;
        private readonly SettingBinding superResolution;
        private JObject configuration;
        private bool isActive;
        private bool isLoading;
        private bool isSaving;
        private int maxFrameGeneration;

        public GraphicSettingView()
        {
            InitializeComponent();
            gamePath = AppDataController.GetGamePath();
            Bind(DDB_FPS, "CustomFrameRate", ("0", "30"), ("1", "45"), ("2", "60"), ("3", "120"));
            Bind(DDB_EnableVSync, "PcVsync", SwitchOptions);
            Bind(DDB_EnableAA, "AntiAliasing", SwitchOptions);
            Bind(DDB_ShadowQuality, "ShadowQuality", ("0", "低"), ("1", "中"), ("2", "高"), ("3", "极高"));
            Bind(DDB_SFXQuality, "NiagaraQuality", ("0", "低"), ("2", "高"));
            Bind(DDB_EnvDetailQuality, "ImageDetail", ("0", "低"), ("1", "中"), ("2", "高"), ("3", "极高"));
            Bind(DDB_AO, "SceneAo", EffectOptions);
            Bind(DDB_VolumeFog, "VolumeFog", EffectOptions);
            Bind(DDB_VolumeLight, "VolumeLight", SwitchOptions);
            Bind(DDB_MotionBlur, "MotionBlur", EffectOptions);
            Bind(DDB_BloomEnable, "BloomEnable", SwitchOptions);
            Bind(DDB_NpcDensity, "NpcDensity", QualityOptions);
            Bind(DDB_EnemyHitDisplayMode, "EnemyHitDisplayMode", SwitchOptions);
            Bind(DDB_AutoAdjust, "AutoAdjustImageQuality", SwitchOptions);
            Bind(DDB_DLSS, "NvidiaSuperSamplingEnable", SwitchOptions);
            Bind(DDB_FrameGeneration, "NvidiaSuperSamplingFrameGenerate",
                ("0", "关闭"), ("1", "2x"), ("2", "3x"), ("3", "4x"), ("4", "5x"), ("5", "6x"));
            superResolution = Bind(DDB_SuperResolution, "NvidiaSuperSamplingQuality", SuperResolutionOptions);
            Bind(DDB_NvidiaReflex, "NvidiaReflex", ("0", "关闭"), ("1", "开启"), ("3", "开启并加速"));
            Bind(DDB_XessEnable, "XessEnable", SwitchOptions);
            Bind(DDB_XessQuality, "XessQuality",
                ("0", "超级性能"), ("1", "性能"), ("2", "平衡"), ("3", "质量"), ("4", "超级质量"));
            Bind(DDB_RayTracing, "RayTracing", EffectOptions);
            Loaded += OnLoaded;
            Unloaded += (_, _) => isActive = false;
        }

        private SettingBinding Bind(DropDownButton button, string key,
            params (string Value, string Text)[] options)
        {
            var setting = new SettingBinding { Button = button, Key = key, Options = options };
            settings.Add(setting);
            return setting;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            isActive = true;
            if (configuration == null) await LoadDataAsync();
        }

        private async void Retry_Click(object sender, RoutedEventArgs e) => await LoadDataAsync(true);

        private async Task LoadDataAsync(bool force = false)
        {
            if (isLoading) return;
            isLoading = true;
            LoadError.Visibility = Visibility.Collapsed;
            DDB_Load.Visibility = Visibility.Visible;
            DDB_Load.IsActive = true;
            try
            {
                await LoadGraphicsDataAsync(force);
                if (isActive) DDB_Main.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Logging.Write($"读取画质设置失败: {ex}", 3, "GraphicSettingView");
                if (isActive)
                {
                    DDB_Main.Visibility = Visibility.Collapsed;
                    LoadError.Visibility = Visibility.Visible;
                }
            }
            finally
            {
                isLoading = false;
                DDB_Load.IsActive = false;
                DDB_Load.Visibility = Visibility.Collapsed;
            }
        }

        private async Task<JObject> ReadConfigurationAsync()
        {
            string output = await ProcessRun.WaveToolsHelperAsync($"/GetGS \"{gamePath}\"", true);
            return ParseConfiguration(output);
        }

        private static JObject ParseConfiguration(string output)
        {
            var data = JObject.Parse(output);
            if (data["ImageQuality"] == null)
                throw new InvalidOperationException("未找到画质设置，请先在游戏内保存设置。");
            return data;
        }

        private async Task LoadGraphicsDataAsync(bool force)
        {
            JObject data = !force && !string.IsNullOrWhiteSpace(StartGameView.GS)
                ? ParseConfiguration(StartGameView.GS) : await ReadConfigurationAsync();
            int limit = data["NvidiaSuperSamplingFrameGenerate"] != null
                ? await FrameGenerationLimit.Value : 0;
            if (!isActive || !IsCurrentGamePath()) return;
            configuration = data;
            maxFrameGeneration = limit;
            StartGameView.GS = data.ToString(Formatting.None);
            RenderSettings();
        }

        private string Value(string key)
        {
            JToken token = configuration?[key];
            return token == null || token.Type == JTokenType.Null ? null : token.ToString();
        }

        private bool Has(string key) => !string.IsNullOrWhiteSpace(Value(key));
        private bool IsCurrentGamePath() =>
            string.Equals(gamePath, AppDataController.GetGamePath(), StringComparison.OrdinalIgnoreCase);

        private void SetRowVisible(DropDownButton button, bool visible)
        {
            if (FindName(button.Name + "_Row") is FrameworkElement row)
                row.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RenderSettings()
        {
            if (configuration == null || !isActive) return;
            bool modernDlss = Has("NvidiaSuperSamplingQuality");
            superResolution.Key = modernDlss ? "NvidiaSuperSamplingQuality" : "NvidiaSuperSamplingMode";
            superResolution.Options = modernDlss ? SuperResolutionOptions : LegacySuperResolutionOptions;

            foreach (var setting in settings)
            {
                string value = Value(setting.Key);
                bool present = !string.IsNullOrWhiteSpace(value);
                SetRowVisible(setting.Button, present);
                setting.Button.IsEnabled = present;
                setting.Button.Content = setting.Options.FirstOrDefault(option => option.Value == value).Text ?? value ?? "";
                var flyout = new MenuFlyout();
                foreach (var option in setting.Options)
                {
                    if (setting.Key == "NvidiaSuperSamplingFrameGenerate" &&
                        int.Parse(option.Value, CultureInfo.InvariantCulture) > maxFrameGeneration)
                        continue;
                    var item = new MenuFlyoutItem
                    {
                        Text = option.Text,
                        Tag = new SettingChoice { Setting = setting, Key = setting.Key, Value = option.Value }
                    };
                    item.Click += ChangeGraphic;
                    flyout.Items.Add(item);
                }
                setting.Button.Flyout = flyout;
            }

            bool dlssEnabled = Value("NvidiaSuperSamplingEnable") == "1";
            DDB_FrameGeneration.IsEnabled = Has("NvidiaSuperSamplingFrameGenerate") && dlssEnabled &&
                (maxFrameGeneration > 0 || Value("NvidiaSuperSamplingFrameGenerate") != "0");
            ToolTipService.SetToolTip(DDB_FrameGeneration, maxFrameGeneration > 0
                ? "可用倍率取决于显卡和驱动" : "无法确认插帧支持情况，可关闭已有插帧");
            DDB_SuperResolution.IsEnabled = Has(superResolution.Key) && dlssEnabled;
            DDB_XessQuality.IsEnabled = Has("XessQuality") && Value("XessEnable") == "1";

            bool frameGenerationEnabled = dlssEnabled &&
                int.TryParse(Value("NvidiaSuperSamplingFrameGenerate"), out int frames) && frames > 0;
            if (frameGenerationEnabled)
            {
                DDB_FPS.Content = "无上限（插帧）";
                DDB_FPS.IsEnabled = false;
            }

            SetRowVisible(DDB_Sharpness, Has("NvidiaSuperSamplingSharpness"));
            bool sharpnessValid = double.TryParse(Value("NvidiaSuperSamplingSharpness"),
                NumberStyles.Float, CultureInfo.InvariantCulture, out double sharpness);
            DDB_Sharpness.Content = sharpnessValid ? FormatNumber(sharpness) : Value("NvidiaSuperSamplingSharpness") ?? "";
            DDB_Sharpness.IsEnabled = dlssEnabled && sharpnessValid && sharpness >= -1 && sharpness <= 1;
            if (sharpnessValid && sharpness >= -1 && sharpness <= 1) SharpnessSlider.Value = sharpness;
            SharpnessValueText.Text = DDB_Sharpness.Content.ToString();

            NvidiaSettings.Visibility = settings.Any(setting =>
                Has(setting.Key) && setting.Key.StartsWith("Nvidia", StringComparison.Ordinal))
                || Has("NvidiaSuperSamplingSharpness") ? Visibility.Visible : Visibility.Collapsed;
            XessSettings.Visibility = Has("XessEnable") || Has("XessQuality")
                ? Visibility.Visible : Visibility.Collapsed;
            DDB_Main.IsEnabled = !isSaving;
        }

        private async void ChangeGraphic(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem { Tag: SettingChoice choice } && choice.Setting.Button.IsEnabled &&
                choice.Key == choice.Setting.Key)
                await SaveSettingAsync(choice.Key, choice.Value);
        }

        private void SharpnessSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (SharpnessValueText != null) SharpnessValueText.Text = FormatNumber(e.NewValue);
        }

        private async void SharpnessApply_Click(object sender, RoutedEventArgs e)
        {
            if (!DDB_Sharpness.IsEnabled) return;
            string value = FormatNumber(SharpnessSlider.Value);
            DDB_Sharpness.Flyout.Hide();
            await SaveSettingAsync("NvidiaSuperSamplingSharpness", value);
        }

        private static string FormatNumber(double value) => (value == 0 ? 0 : value).ToString("0.##", CultureInfo.InvariantCulture);

        private async Task SaveSettingAsync(string key, string value)
        {
            if (isSaving || !isActive || !Has(key)) return;
            isSaving = true;
            DDB_Main.IsEnabled = false;
            try
            {
                if (!IsCurrentGamePath()) throw new InvalidOperationException("游戏路径已改变，请刷新画质设置。");
                if (IsGameRunning()) throw new InvalidOperationException("请先退出游戏再修改画质。");
                bool rayChildrenMatch = key != "RayTracing" ||
                    new[] { "RayTracedGI", "RayTracedReflection", "RayTracedShadow" }
                        .All(child => Value(child) == (value == "0" ? "0" : "1"));
                if (SameValue(Value(key), value) && rayChildrenMatch) return;
                string command = key == "RayTracing"
                    ? $"/SetRayTracing \"{gamePath}\" {value}"
                    : $"/SetGS \"{gamePath}\" {key} {value}";
                await ProcessRun.WaveToolsHelperAsync(command, true);
                JObject saved = await ReadConfigurationAsync();
                if (!SameValue(saved[key]?.ToString(), value))
                    throw new InvalidOperationException("画质参数未保存，请更新 WaveToolsHelper 后重试。");
                if (key == "RayTracing")
                {
                    string enabled = value == "0" ? "0" : "1";
                    foreach (string child in new[] { "RayTracedGI", "RayTracedReflection", "RayTracedShadow" })
                        if (saved[child]?.ToString() != enabled)
                            throw new InvalidOperationException("光追子项未同步，请更新 WaveToolsHelper 后重试。");
                }
                if (IsCurrentGamePath())
                {
                    StartGameView.GS = saved.ToString(Formatting.None);
                    configuration = saved;
                }
            }
            catch (Exception ex)
            {
                Logging.Write($"保存画质设置失败: {ex}", 3, "GraphicSettingView");
                if (isActive)
                    NotificationManager.RaiseNotification("画质设置保存失败", ex.Message, InfoBarSeverity.Warning, false, 5);
                if (isActive && IsCurrentGamePath())
                {
                    try { await LoadGraphicsDataAsync(true); }
                    catch (Exception readError) { Logging.Write($"重新读取画质失败: {readError.Message}", 3, "GraphicSettingView"); }
                }
            }
            finally
            {
                isSaving = false;
                if (isActive) RenderSettings();
            }
        }

        private static bool SameValue(string left, string right) =>
            string.Equals(left, right, StringComparison.Ordinal) ||
            double.TryParse(left, NumberStyles.Float, CultureInfo.InvariantCulture, out double a) &&
            double.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out double b) && a == b;

        private static bool IsGameRunning()
        {
            Process[] processes = Process.GetProcessesByName("Client-Win64-Shipping");
            bool running = processes.Length > 0;
            foreach (var process in processes) process.Dispose();
            return running;
        }

        private static int DetectFrameGenerationLimit()
        {
            int limit = 0;
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                using var adapters = searcher.Get();
                foreach (ManagementObject adapter in adapters)
                {
                    using (adapter)
                    {
                        string name = adapter["Name"]?.ToString() ?? "";
                        if (!name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)) continue;
                        if (Regex.IsMatch(name, @"\bRTX\s*50\d{2}\b", RegexOptions.IgnoreCase) ||
                            Regex.IsMatch(name, @"\bRTX\s+PRO\b.*\bBlackwell\b", RegexOptions.IgnoreCase))
                            limit = Math.Max(limit, 5);
                        else if (Regex.IsMatch(name, @"\bRTX\s*40\d{2}\b", RegexOptions.IgnoreCase) ||
                                 Regex.IsMatch(name, @"\bRTX\b.*\bAda\s+Generation\b", RegexOptions.IgnoreCase))
                            limit = Math.Max(limit, 1);
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Write($"读取插帧显卡支持情况失败: {ex.Message}", 2, "GraphicSettingView");
            }
            return limit;
        }
    }
}
