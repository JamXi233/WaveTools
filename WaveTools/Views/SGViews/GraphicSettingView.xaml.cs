using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WaveTools.Depend;
using WaveTools.Depend;

namespace WaveTools.Views.SGViews
{
    public sealed partial class GraphicSettingView : Page
    {
        public GraphicSettingView()
        {
            this.InitializeComponent();
            Logging.Write("Switch to GraphicSettingView", 0);
            LoadData();
        }

        private async void LoadData()
        {
            await LoadGraphicsData(false);
            DDB_Main.Visibility = Visibility.Visible;
            DDB_Load.Visibility = Visibility.Collapsed;
        }

        private async Task LoadGraphicsData(bool isForce)
        {
            string output;
            string returnValue;

            if (isForce)
            {
                output = await ProcessRun.WaveToolsHelperAsync($"/GetGS {AppDataController.GetGamePathForHelper()}");
                returnValue = output.Trim();
            }
            else
            {
                if (StartGameView.GS is not null)
                {
                    returnValue = StartGameView.GS;
                }
                else
                {
                    output = await ProcessRun.WaveToolsHelperAsync($"/GetGS {AppDataController.GetGamePathForHelper()}");
                    returnValue = output.Trim();
                }
            }

            // 使用 returnValue 变量进行后续处理
            JObject config = JObject.Parse(returnValue);

            // 设置UI控件的值
            SetUIValue(config, "CustomFrameRate", DDB_FPS, new Dictionary<string, string> { { "30", "30" }, { "45", "45" }, { "60", "60" }, { "120", "120" }, { "240", "240" }, { "320", "320" } });
            SetUIValue(config, "PcVsync", DDB_EnableVSync, new Dictionary<string, string> { { "0", "关闭" }, { "1", "开启" } });
            SetUIValue(config, "AntiAliasing", DDB_EnableAA, new Dictionary<string, string> { { "0", "关闭" }, { "1", "开启" } });
            SetUIValue(config, "ShadowQuality", DDB_ShadowQuality, new Dictionary<string, string> { { "0", "低" }, { "1", "中" }, { "2", "高" }, { "3", "极高" } });
            SetUIValue(config, "NiagaraQuality", DDB_SFXQuality, new Dictionary<string, string> { { "0", "低" }, { "1", "中" }, { "2", "高" } });
            SetUIValue(config, "ImageDetail", DDB_EnvDetailQuality, new Dictionary<string, string> { { "0", "低" }, { "1", "中" }, { "2", "高" } });
            SetUIValue(config, "SceneAo", DDB_AO, new Dictionary<string, string> { { "0", "关闭" }, { "1", "开启" } });
            SetUIValue(config, "VolumeFog", DDB_VolumeFog, new Dictionary<string, string> { { "0", "关闭" }, { "1", "开启" } });
            SetUIValue(config, "VolumeLight", DDB_VolumeLight, new Dictionary<string, string> { { "0", "关闭" }, { "1", "开启" } });
            SetUIValue(config, "MotionBlur", DDB_MotionBlur, new Dictionary<string, string> { { "0", "关闭" }, { "1", "开启" } });
            SetUIValue(config, "NvidiaSuperSamplingEnable", DDB_DLSS, new Dictionary<string, string> { { "0", "关闭" }, { "1", "开启" } });
            SetUIValue(config, "NvidiaSuperSamplingMode", DDB_SuperResolution, new Dictionary<string, string>
    {
        { "0", "关闭" }, { "1", "自动" }, { "3", "质量" }, { "4", "平衡" },
        { "5", "性能" }, { "6", "超级性能" }
    });
            SetUIValue(config, "NvidiaSuperSamplingSharpness", DDB_Sharpness);
            SetUIValue(config, "BloomEnable", DDB_BloomEnable, new Dictionary<string, string> { { "0", "关闭" }, { "1", "开启" } });
            SetUIValue(config, "NvidiaReflex", DDB_NvidiaReflex, new Dictionary<string, string> { { "0", "关闭" }, { "1", "开启" } });
            SetUIValue(config, "NpcDensity", DDB_NpcDensity, new Dictionary<string, string> { { "0", "低" }, { "1", "中" }, { "2", "高" } });
            SetUIValue(config, "EnemyHitDisplayMode", DDB_EnemyHitDisplayMode, new Dictionary<string, string> { { "0", "关闭" }, { "1", "开启" } });
        }


        private void SetUIValue(JObject config, string key, DropDownButton button, Dictionary<string, string> map = null)
        {
            if (config.TryGetValue(key, out JToken value))
            {
                string valueStr = value.Type == JTokenType.Integer ? value.ToObject<int>().ToString() : value.ToString();
                if (map != null)
                {
                    if (map.TryGetValue(valueStr, out string text))
                    {
                        button.Content = text;
                    }
                    else
                    {
                        button.Content = valueStr;
                    }
                }
                else
                {
                    button.Content = valueStr;
                }
            }
        }

        private async void ChangeGraphic(object sender, RoutedEventArgs e)
        {
            MenuFlyoutItem item = sender as MenuFlyoutItem;
            string text = item.Text;
            string tag = item.Tag.ToString();

            try
            {
                // 先应用用户选择
                ApplyUserChoice(text, tag);

                // 异步更新画质设置
                var settingsMap = new Dictionary<string, Dictionary<string, string>>
        {
            { "ShadowQuality", new Dictionary<string, string> { { "低", "0" }, { "中", "1" }, { "高", "2" }, { "极高", "3" } } },
            { "NiagaraQuality", new Dictionary<string, string> { { "低", "0" }, { "中", "1" }, { "高", "2" } } },
            { "ImageDetail", new Dictionary<string, string> { { "低", "0" }, { "中", "1" }, { "高", "2" } } },
            { "SceneAo", new Dictionary<string, string> { { "关闭", "0" }, { "开启", "1" } } },
            { "VolumeFog", new Dictionary<string, string> { { "关闭", "0" }, { "开启", "1" } } },
            { "VolumeLight", new Dictionary<string, string> { { "关闭", "0" }, { "开启", "1" } } },
            { "MotionBlur", new Dictionary<string, string> { { "关闭", "0" }, { "开启", "1" } } },
            { "NvidiaSuperSamplingEnable", new Dictionary<string, string> { { "关闭", "0" }, { "开启", "1" } } },
            { "NvidiaSuperSamplingMode", new Dictionary<string, string> { { "关闭", "0" }, { "自动", "1" }, { "质量", "3" }, { "平衡", "4" }, { "性能", "5" }, { "超级性能", "6" } } },
            { "PcVsync", new Dictionary<string, string> { { "关闭", "0" }, { "开启", "1" } } },
            { "AntiAliasing", new Dictionary<string, string> { { "关闭", "0" }, { "开启", "1" } } },
            { "BloomEnable", new Dictionary<string, string> { { "关闭", "0" }, { "开启", "1" } } },
            { "NvidiaReflex", new Dictionary<string, string> { { "关闭", "0" }, { "开启", "1" } } },
            { "NpcDensity", new Dictionary<string, string> { { "低", "0" }, { "中", "1" }, { "高", "2" } } },
            { "EnemyHitDisplayMode", new Dictionary<string, string> { { "关闭", "0" }, { "开启", "1" } } }
        };

                if (settingsMap.ContainsKey(tag) && settingsMap[tag].ContainsKey(text))
                {
                    string value = settingsMap[tag][text];
                    await ProcessRun.WaveToolsHelperAsync($"/SetGS {AppDataController.GetGamePathForHelper()} {tag} {value}");
                }
                else if (double.TryParse(text, out double sharpnessValue) && tag == "KeyNvidiaSuperSamplingSharpness")
                {
                    await ProcessRun.WaveToolsHelperAsync($"/SetGS {AppDataController.GetGamePathForHelper()} {tag} {sharpnessValue}");
                }
                else if (text.Contains("30") || text.Contains("45") || text.Contains("60") || text.Contains("120"))
                {
                    await ProcessRun.WaveToolsHelperAsync($"/SetGS {AppDataController.GetGamePathForHelper()} {tag} {text}");
                }

                // 确保设置完成后重新加载数据
                await LoadGraphicsData(true);
            }
            catch (Exception ex)
            {
                Logging.Write($"Error in ChangeGraphic: {ex.Message}", 2);
            }
        }

        private void ApplyUserChoice(string text, string tag)
        {
            // 根据选择直接更新UI
            switch (tag)
            {
                case "CustomFrameRate":
                    DDB_FPS.Content = text;
                    break;
                case "PcVsync":
                    DDB_EnableVSync.Content = text;
                    break;
                case "AntiAliasing":
                    DDB_EnableAA.Content = text;
                    break;
                case "ShadowQuality":
                    DDB_ShadowQuality.Content = text;
                    break;
                case "NiagaraQuality":
                    DDB_SFXQuality.Content = text;
                    break;
                case "ImageDetail":
                    DDB_EnvDetailQuality.Content = text;
                    break;
                case "SceneAo":
                    DDB_AO.Content = text;
                    break;
                case "VolumeFog":
                    DDB_VolumeFog.Content = text;
                    break;
                case "VolumeLight":
                    DDB_VolumeLight.Content = text;
                    break;
                case "MotionBlur":
                    DDB_MotionBlur.Content = text;
                    break;
                case "NvidiaSuperSamplingEnable":
                    DDB_DLSS.Content = text;
                    break;
                case "NvidiaSuperSamplingMode":
                    DDB_SuperResolution.Content = text;
                    break;
                case "NvidiaSuperSamplingSharpness":
                    DDB_Sharpness.Content = text;
                    break;
                case "BloomEnable":
                    DDB_BloomEnable.Content = text;
                    break;
                case "NvidiaReflex":
                    DDB_NvidiaReflex.Content = text;
                    break;
                case "NpcDensity":
                    DDB_NpcDensity.Content = text;
                    break;
                case "EnemyHitDisplayMode":
                    DDB_EnemyHitDisplayMode.Content = text;
                    break;
            }
        }
    }
}
