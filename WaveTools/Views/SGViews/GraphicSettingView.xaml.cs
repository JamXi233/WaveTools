using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
            await LoadGraphicsData();
            DDB_Main.Visibility = Visibility.Visible;
            DDB_Load.Visibility = Visibility.Collapsed;
        }

        private async Task LoadGraphicsData()
        {
            string output = await ProcessRun.WaveToolsHelperAsync($"/GetGS {AppDataController.GetGamePathForHelper()}");
            string returnValue = output.Trim(); // 去掉字符串末尾的换行符和空格

            // 使用 returnValue 变量进行后续处理
            JObject config = JObject.Parse(returnValue);

            // 设置UI控件的值
            SetUIValue(config, "KeyCustomFrameRate", DDB_FPS, new Dictionary<int, string> { { 30, "30" }, { 45, "45" }, { 60, "60" }, { 120, "120" } });
            SetUIValue(config, "KeyPcVsync", DDB_EnableVSync, new Dictionary<int, string> { { 0, "关闭" }, { 1, "开启" } });
            SetUIValue(config, "KeyAntiAliasing", DDB_EnableAA, new Dictionary<int, string> { { 0, "关闭" }, { 1, "开启" } });
            SetUIValue(config, "KeyNewShadowQuality", DDB_ShadowQuality, new Dictionary<int, string> { { 0, "低" }, { 1, "中" }, { 2, "高" }, { 3, "极高" } });
            SetUIValue(config, "KeyNiagaraQuality", DDB_SFXQuality, new Dictionary<int, string> { { 0, "低" }, { 1, "高" } });
            SetUIValue(config, "KeyImageDetail", DDB_EnvDetailQuality, new Dictionary<int, string> { { 0, "低" }, { 1, "中" }, { 2, "高" } });
            SetUIValue(config, "KeySceneAo", DDB_AO, new Dictionary<int, string> { { 0, "关闭" }, { 1, "开启" } });
            SetUIValue(config, "KeyVolumeFog", DDB_VolumeFog, new Dictionary<int, string> { { 0, "关闭" }, { 1, "开启" } });
            SetUIValue(config, "KeyVolumeLight", DDB_VolumeLight, new Dictionary<int, string> { { 0, "关闭" }, { 1, "开启" } });
            SetUIValue(config, "KeyMotionBlur", DDB_MotionBlur, new Dictionary<int, string> { { 0, "关闭" }, { 1, "开启" } });
            SetUIValue(config, "KeyNvidiaSuperSamplingEnable", DDB_DLSS, new Dictionary<int, string> { { 0, "关闭" }, { 1, "开启" } });
            SetUIValue(config, "KeyNvidiaSuperSamplingMode", DDB_SuperResolution, new Dictionary<int, string>
            {
                { 0, "关闭" }, { 1, "自动" }, { 3, "质量" }, { 4, "平衡" },
                { 5, "性能" }, { 6, "超级性能" }
            });
            SetUIValue(config, "KeyNvidiaSuperSamplingSharpness", DDB_Sharpness);
            SetUIValue(config, "KeyNvidiaReflex", DDB_NvidiaReflex, new Dictionary<int, string> { { 0, "关闭" }, { 1, "开启" } });
        }

        private void SetUIValue(JObject config, string key, DropDownButton button, Dictionary<int, string> map = null)
        {
            if (config.TryGetValue(key, out JToken value))
            {
                if (map != null && value.Type == JTokenType.Integer)
                {
                    if (map.TryGetValue(value.ToObject<int>(), out string text))
                    {
                        button.Content = text;
                    }
                    else
                    {
                        button.Content = value.ToString();
                    }
                }
                else
                {
                    button.Content = value.Type == JTokenType.Integer ? value.ToObject<int>().ToString() : value.ToString();
                }
            }
        }

        private void SetUIValue(JObject config, string key, DropDownButton button)
        {
            if (config.TryGetValue(key, out JToken value))
            {
                button.Content = value.Type == JTokenType.Integer ? value.ToObject<int>().ToString() : value.ToString();
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
                await Task.Run(async () =>
                {
                    var settingsMap = new Dictionary<string, Dictionary<string, int>>
                    {
                        { "KeyNewShadowQuality", new Dictionary<string, int> { { "低", 0 }, { "中", 1 }, { "高", 2 }, { "极高", 3 } } },
                        { "KeyNiagaraQuality", new Dictionary<string, int> { { "低", 0 }, { "高", 1 } } },
                        { "KeyImageDetail", new Dictionary<string, int> { { "低", 0 }, { "中", 1 }, { "高", 2 } } },
                        { "KeySceneAo", new Dictionary<string, int> { { "关闭", 0 }, { "开启", 1 } } },
                        { "KeyVolumeFog", new Dictionary<string, int> { { "关闭", 0 }, { "开启", 1 } } },
                        { "KeyVolumeLight", new Dictionary<string, int> { { "关闭", 0 }, { "开启", 1 } } },
                        { "KeyMotionBlur", new Dictionary<string, int> { { "关闭", 0 }, { "开启", 1 } } },
                        { "KeyNvidiaSuperSamplingEnable", new Dictionary<string, int> { { "关闭", 0 }, { "开启", 1 } } },
                        { "KeyNvidiaSuperSamplingMode", new Dictionary<string, int> { { "关闭", 0 }, { "自动", 1 }, { "质量", 3 }, { "平衡", 4 }, { "性能", 5 }, { "超级性能", 6 } } },
                        { "KeyPcVsync", new Dictionary<string, int> { { "关闭", 0 }, { "开启", 1 } } },
                        { "KeyNvidiaReflex", new Dictionary<string, int> { { "关闭", 0 }, { "开启", 1 } } }
                    };

                    if (settingsMap.ContainsKey(tag) && settingsMap[tag].ContainsKey(text))
                    {
                        int value = settingsMap[tag][text];
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
                });

                // 后台更新UI
                _ = LoadGraphicsData();
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
                case "KeyCustomFrameRate":
                    DDB_FPS.Content = text;
                    break;
                case "KeyPcVsync":
                    DDB_EnableVSync.Content = text;
                    break;
                case "KeyAntiAliasing":
                    DDB_EnableAA.Content = text;
                    break;
                case "KeyNewShadowQuality":
                    DDB_ShadowQuality.Content = text;
                    break;
                case "KeyNiagaraQuality":
                    DDB_SFXQuality.Content = text;
                    break;
                case "KeyImageDetail":
                    DDB_EnvDetailQuality.Content = text;
                    break;
                case "KeySceneAo":
                    DDB_AO.Content = text;
                    break;
                case "KeyVolumeFog":
                    DDB_VolumeFog.Content = text;
                    break;
                case "KeyVolumeLight":
                    DDB_VolumeLight.Content = text;
                    break;
                case "KeyMotionBlur":
                    DDB_MotionBlur.Content = text;
                    break;
                case "KeyNvidiaSuperSamplingEnable":
                    DDB_DLSS.Content = text;
                    break;
                case "KeyNvidiaSuperSamplingMode":
                    DDB_SuperResolution.Content = text;
                    break;
                case "KeyNvidiaSuperSamplingSharpness":
                    DDB_Sharpness.Content = text;
                    break;
                case "KeyNvidiaReflex":
                    DDB_NvidiaReflex.Content = text;
                    break;
            }
        }
    }
}
