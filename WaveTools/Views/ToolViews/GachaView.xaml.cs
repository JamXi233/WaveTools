using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using SRTools.Depend;
using WaveTools.Depend;
using Windows.Graphics;
using WinRT.Interop;
using static WaveTools.App;

namespace WaveTools.Views.ToolViews
{
    public sealed partial class GachaView : Page
    {
        public bool isProxyRunning;
        public static String selectedUid;
        public static int selectedCardPoolId;
        public String GachaLink_String;
        public String GachaLinkCache_String;
        public bool isClearGachaSaved;

        private bool isUserInteraction = false;
        private string latestUpdatedUID = null;


        public GachaView()
        {
            InitializeComponent();
            Logging.Write("Switch to GachaView", 0);
            this.Loaded += GachaView_Loaded;
        }

        private async void GachaView_Loaded(object sender, RoutedEventArgs e)
        {
            if (AppDataController.GetGamePath() == "Null") { GetGachaURL.IsEnabled = false; UpdateGacha.IsEnabled = false; noGamePathFound.Visibility = Visibility.Visible; }
            else { noGamePathFound.Visibility = Visibility.Collapsed; await LoadUIDs(); }
        }

        private async void GetGachaURL_Click(object sender, RoutedEventArgs e)
        {

            string recordsBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"JSG-LLC\WaveTools\GachaRecords");
            string gachaLinksJson = await ProcessRun.WaveToolsHelperAsync($"/GetGachaURL {AppDataController.GetGamePathForHelper()}");
            var gachaUrls = JsonConvert.DeserializeObject<List<GachaUrl>>(gachaLinksJson);

            // 创建新对话框
            var dialog = new ContentDialog
            {
                Title = "选择UID",
                PrimaryButtonText = "确认",
                CloseButtonText = "取消",
                XamlRoot = XamlRoot,
                Width = 300
            };

            var stackPanel = new StackPanel
            {
                Width = 300,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Spacing = 2
            };

            stackPanel.Children.Add(new TextBlock { Text = "注：未显示已保存的UID", TextAlignment = TextAlignment.Left });

            var comboBox = new ComboBox
            {
                Width = 300,
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            if (gachaUrls.Count == 0)
            {
                var noFound = new TextBlock { Text = "未找到新的抽卡记录\n请到游戏内打开一次抽卡记录", TextAlignment = TextAlignment.Left };
                stackPanel.Children.Add(noFound);
                comboBox.IsEnabled = false;
            }
            else
            {
                var items = new List<string>();
                foreach (var url in gachaUrls)
                {
                    string playerFilePath = Path.Combine(recordsBasePath, $"{url.PlayerId}.json");
                    if (!File.Exists(playerFilePath))
                    {
                        items.Add(url.PlayerId);
                    }
                }

                if (items.Count == 0)
                {
                    var noFound = new TextBlock { Text = "未找到新的抽卡记录", TextAlignment = TextAlignment.Left };
                    stackPanel.Children.Add(noFound);
                    comboBox.IsEnabled = false;
                }
                else
                {
                    comboBox.ItemsSource = items;
                    comboBox.SelectedIndex = 0;
                }
            }

            stackPanel.Children.Add(comboBox);
            dialog.Content = stackPanel;

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary && comboBox.SelectedItem != null && comboBox.IsEnabled)
            {
                string selectedUid = comboBox.SelectedItem as string;
                SaveGachaLink(selectedUid);
            }
        }

        private async void OpenGachaWeb_Click(object sender, RoutedEventArgs e)
        {
            string gachaUrl = await ProcessRun.WaveToolsHelperAsync($"/GetSavedGachaURL {selectedUid}");

            var newWindow = new Window();
            newWindow.Title = $"[UID:{selectedUid}]抽卡记录";

            var webView = new WebView2
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var grid = new Grid();
            grid.Children.Add(webView);

            newWindow.Content = grid;

            // 获取 AppWindow 对象并设置窗口大小
            IntPtr hWnd = WindowNative.GetWindowHandle(newWindow);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            appWindow.Resize(new SizeInt32(951, 534));

            newWindow.Activate();

            // 初始化 WebView2 并禁用开发者工具和右键菜单
            await webView.EnsureCoreWebView2Async(null);

            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.Source = new Uri(gachaUrl);
        }



        private async void UpdateGacha_Click(object sender, RoutedEventArgs e)
        {
            UpdateGacha_R(selectedUid);
        }

        private async void SaveGachaLink(string UID)
        {
            WaitOverlayManager.RaiseWaitOverlay(true, true, 0, "正在保存抽卡链接", "请稍等片刻");
            await ProcessRun.WaveToolsHelperAsync($"/SaveGachaURL {AppDataController.GetGamePathForHelper()} {UID}");
            WaitOverlayManager.RaiseWaitOverlay(false);
            await GetGachaRecords(UID);
            ReloadGachaView();
        }

        private async Task GetGachaRecords(string UID)
        {
            WaitOverlayManager.RaiseWaitOverlay(true, true, 0, "正在获取抽卡记录", "请稍等片刻");
            await ProcessRun.WaveToolsHelperAsync($"/GetGachaRecords {UID}");
            WaitOverlayManager.RaiseWaitOverlay(false);
            ReloadGachaView();
        }

        private async void UpdateGacha_R(string UID)
        {
            if (UID is null) { NotificationManager.RaiseNotification($"更新抽卡记录", "更新记录失败:UID为空", InfoBarSeverity.Error); return; }
            WaitOverlayManager.RaiseWaitOverlay(true, true, 0, "正在更新抽卡记录", "请稍等片刻");
            await ProcessRun.WaveToolsHelperAsync($"/UpdateGachaRecords {UID}");
            latestUpdatedUID = UID;
            WaitOverlayManager.RaiseWaitOverlay(false);
            ReloadGachaView();
        }

        private async void ReloadGachaView(TeachingTip sender = null, object args = null)
        {
            GachaRecordsUID.SelectionChanged -= GachaRecordsUID_SelectionChanged; // 临时禁用事件处理程序
            await LoadUIDs();
            GachaRecordsUID.SelectionChanged += GachaRecordsUID_SelectionChanged; // 重新启用事件处理程序

            // 重新加载其他UI元素
            ReloadGachaMainView();

            // 切换到最新更新的UID
            if (!string.IsNullOrEmpty(latestUpdatedUID))
            {
                isUserInteraction = false; // 设置为程序自动选择
                GachaRecordsUID.SelectedItem = latestUpdatedUID;
                latestUpdatedUID = null; // 重置变量
            }
            isUserInteraction = true;
        }

        private void ComboBox_Click(object sender, object e)
        {
            isUserInteraction = true;
        }

        // 新增的方法，用于重新加载 ComboBox (GachaRecordsUID)
        private void ReloadGachaComboBox()
        {
            selectedUid = null;
            GachaRecordsUID.SelectionChanged -= GachaRecordsUID_SelectionChanged; // 临时禁用事件处理程序
            GachaRecordsUID.ItemsSource = null;
            GachaRecordsUID.Items.Clear();

            string recordsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JSG-LLC", "WaveTools", "GachaLinks");
            if (Directory.Exists(recordsDirectory))
            {
                var uidFiles = Directory.GetFiles(recordsDirectory, "*.json");
                var uidList = uidFiles.Select(Path.GetFileNameWithoutExtension).ToList();
                GachaRecordsUID.ItemsSource = uidList;
                if (uidList.Count > 0)
                {
                    GachaRecordsUID.SelectedIndex = 0;
                    selectedUid = GachaRecordsUID.SelectedValue.ToString();
                }
            }
            GachaRecordsUID.SelectionChanged += GachaRecordsUID_SelectionChanged; // 重新启用事件处理程序
        }

        // 新增的方法，用于重新加载整个界面
        private void ReloadGachaMainView()
        {
            isUserInteraction = false;
            // 先重新加载 ComboBox (GachaRecordsUID)
            ReloadGachaComboBox();

            // 根据ComboBox的项数决定是否显示gachaView
            if (GachaRecordsUID.Items.Count == 0)
            {
                gachaView.Visibility = Visibility.Collapsed;
            }
            else
            {
                gachaView.Visibility = Visibility.Visible;
            }

            // 重新加载其他可能需要重新加载的元素
            // 例如，如果有其他控件需要更新，可以在这里添加相应的更新逻辑
            // 假设有一个名为 gachaFrame 的 Frame 控件
            if (gachaFrame != null)
            {
                gachaFrame.Navigate(typeof(GachaViews.TempGachaView), selectedUid);
            }

            // 清空并重新加载 gachaNav
            gachaNav.Items.Clear();
            if (!string.IsNullOrEmpty(selectedUid))
            {
                LoadGachaRecords(selectedUid);
            }

            // 重新加载清除按钮的状态
            ClearGacha.IsEnabled = !string.IsNullOrEmpty(selectedUid);
            OpenGachaWeb.IsEnabled = !string.IsNullOrEmpty(selectedUid);

            // 更新 UI 的其他部分（例如，显示或隐藏特定的控件）
            loadGachaProgress.Visibility = Visibility.Collapsed;
            noGachaFound.Visibility = Visibility.Collapsed;
        }

        private async Task LoadUIDs()
        {
            GachaRecordsUID.ItemsSource = null;
            try
            {
                string recordsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JSG-LLC", "WaveTools", "GachaLinks");

                if (!Directory.Exists(recordsDirectory))
                {
                    //NotificationManager.RaiseNotification($"抽卡分析", "无抽卡记录", InfoBarSeverity.Warning);
                    loadGachaProgress.Visibility = Visibility.Collapsed;
                    noGachaFound.Visibility = Visibility.Visible;
                    return;
                }

                var uidFiles = Directory.GetFiles(recordsDirectory, "*.json");
                if (uidFiles.Length == 0)
                {
                    //NotificationManager.RaiseNotification($"抽卡分析", "无抽卡记录", InfoBarSeverity.Warning);
                    loadGachaProgress.Visibility = Visibility.Collapsed;
                    noGachaFound.Visibility = Visibility.Visible;
                    return;
                }

                var uidList = uidFiles.Select(Path.GetFileNameWithoutExtension).ToList();
                GachaRecordsUID.ItemsSource = uidList;
                if (uidList.Count > 0)
                {
                    GachaRecordsUID.SelectedIndex = 0;
                    loadGachaProgress.Visibility = Visibility.Collapsed;
                    noGachaFound.Visibility = Visibility.Collapsed;
                    gachaView.Visibility = Visibility;
                    ClearGacha.IsEnabled = true;
                    OpenGachaWeb.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                NotificationManager.RaiseNotification($"抽卡分析", "加载UID时出现错误", InfoBarSeverity.Error);
            }
        }

        private void GachaRecordsUID_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GachaRecordsUID.SelectedItem != null)
            {
                selectedUid = GachaRecordsUID.SelectedItem.ToString();
                if (!string.IsNullOrEmpty(selectedUid))
                {
                    if (isUserInteraction)
                    {
                        // 用户手动选择
                        LoadGachaRecords(selectedUid);
                    }
                    else
                    {
                        // 程序自动选择
                        Console.WriteLine("程序自动选择了UID: " + selectedUid);
                        LoadGachaRecords(selectedUid);
                        isUserInteraction = true; // 重置标志变量
                    }
                }
                else
                {
                    Console.WriteLine("Selected UID 为空");
                }
            }
        }

        private async void LoadGachaRecords(string uid)
        {
            try
            {
                string recordsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JSG-LLC", "WaveTools", "GachaRecords");
                string filePath = Path.Combine(recordsDirectory, $"{uid}.json");

                if (!File.Exists(filePath))
                {
                    if (isUserInteraction)
                    {
                        // 用户手动选择的情况，弹出对话框
                        DialogManager.RaiseDialog(true, "抽卡记录未找到", $"未找到UID:{uid}的抽卡记录文件\n需要更新抽卡记录", true, "更新", () => { UpdateGacha_R(uid); });
                        gachaNav.Visibility = Visibility.Collapsed;
                        gachaFrame.Visibility = Visibility.Collapsed;
                        return;
                    }

                    // 使用for循环遍历所有选项
                    bool recordFound = false;
                    isUserInteraction = false; // 设置为程序自动选择
                    GachaRecordsUID.SelectionChanged -= GachaRecordsUID_SelectionChanged; // 临时禁用事件处理程序
                    for (int i = 0; i < GachaRecordsUID.Items.Count; i++)
                    {
                        GachaRecordsUID.SelectedIndex = i;
                        var newUid = GachaRecordsUID.SelectedItem.ToString();
                        string newFilePath = Path.Combine(recordsDirectory, $"{newUid}.json");

                        if (File.Exists(newFilePath))
                        {
                            recordFound = true;
                            break;
                        }
                    }

                    if (!recordFound)
                    {
                        // 回到第一个项目并弹出通知
                        GachaRecordsUID.SelectedIndex = 0;
                        NotificationManager.RaiseNotification("无可用的抽卡记录", "", InfoBarSeverity.Warning);
                        gachaNav.Visibility = Visibility.Collapsed;
                        gachaFrame.Visibility = Visibility.Collapsed;
                    }
                    GachaRecordsUID.SelectionChanged += GachaRecordsUID_SelectionChanged; // 重新启用事件处理程序
                    return;
                }

                var jsonContent = await File.ReadAllTextAsync(filePath);
                var gachaData = JsonConvert.DeserializeObject<GachaData>(jsonContent);

                DisplayGachaData(gachaData);
            }
            catch (Exception ex)
            {
                NotificationManager.RaiseNotification("加载抽卡记录时发生错误", $"{ex.Message}", InfoBarSeverity.Error);
            }
        }

        private void DisplayGachaData(GachaData gachaData)
        {
            gachaNav.Items.Clear();

            if (gachaData?.List == null || gachaData.List.Count == 0)
            {
                // 显示无记录信息
                return;
            }

            SelectorBarItem firstEnabledItem = null;

            foreach (var pool in gachaData.List)
            {
                var item = new SelectorBarItem
                {
                    Text = pool.CardPoolType,
                    Tag = pool.CardPoolId.ToString(),
                    IsEnabled = pool.Records != null && pool.Records.Count > 0
                };

                if (item.IsEnabled && firstEnabledItem == null)
                {
                    firstEnabledItem = item;
                }

                item.Tapped += SelectorBarItem_Tapped; // 添加点击事件处理程序
                gachaNav.Items.Add(item);
            }

            if (firstEnabledItem != null)
            {
                gachaNav.Visibility = Visibility.Visible;
                gachaFrame.Visibility = Visibility.Visible;
                firstEnabledItem.IsSelected = true;
                LoadGachaPoolData(firstEnabledItem.Tag.ToString());
            }
        }

        private void SelectorBarItem_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (sender is SelectorBarItem item)
            {
                string tag = item.Tag.ToString();
                Console.WriteLine($"Selected Card Pool: {tag}");
                selectedCardPoolId = int.Parse(tag);
                LoadGachaPoolData(tag);
            }
        }

        private void LoadGachaPoolData(string cardPoolId)
        {
            // 切换到新选择的池子时重新加载frame
            selectedCardPoolId = int.Parse(cardPoolId);
            if (gachaFrame != null)
            {
                gachaFrame.Navigate(typeof(GachaViews.TempGachaView), selectedUid);
            }
        }

        private void ExportWWGF_Click(object sender, RoutedEventArgs e)
        {
            // 导出记录逻辑
        }

        private async void ImportWWGF_Click(object sender, RoutedEventArgs e)
        {
            // 导入记录逻辑
        }

        private async void ClearGacha_Click(object sender, RoutedEventArgs e)
        {
            
            ClearGachaText.Text = $"将清空的UID:{selectedUid}";
            ClearGachaTip.IsOpen = true;
        }

        private async void ConfirmClearGachaRecords(object sender, RoutedEventArgs e)
        {
            await ClearGacha_Run(0);
            ClearGachaTip.IsOpen = false;
        }

        private async void ConfirmClearGachaRecordsAndUID(object sender, RoutedEventArgs e)
        {
            await ClearGacha_Run(1);
            ClearGachaTip.IsOpen = false;
        }


        private async Task ClearGacha_Run(int clearGachaMode)
        {
            if (clearGachaMode == 0)
            {
                await ProcessRun.WaveToolsHelperAsync($"/DeleteGachaRecords {selectedUid}");
            }
            else if (clearGachaMode == 1)
            {
                await ProcessRun.WaveToolsHelperAsync($"/DeleteGachaRecords {selectedUid}");
                await ProcessRun.WaveToolsHelperAsync($"/DeleteGachaUID {selectedUid}");
            }
            // 重新加载其他UI元素
            isUserInteraction = false;
            ReloadGachaMainView();
        }

        public Border CreateDetailBorder()
        {
            return new Border
            {
                Padding = new Thickness(3),
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };
        }
    }

    

    public class GachaData
    {
        public GachaInfo Info { get; set; }
        public List<GachaPool> List { get; set; }
    }

    public class GachaInfo
    {
        public string Uid { get; set; }
    }

    public class GachaPool
    {
        public int CardPoolId { get; set; }
        public string CardPoolType { get; set; }
        public List<GachaRecord> Records { get; set; }
    }

    public class GachaRecord
    {
        public string ResourceId { get; set; }
        public string Name { get; set; }
        public int QualityLevel { get; set; }
        public string ResourceType { get; set; }
        public string Time { get; set; }
    }

    public class GachaUrl
    {
        [JsonProperty("gachaLink")]
        public string GachaLink { get; set; }

        [JsonProperty("playerId")]
        public string PlayerId { get; set; }

        [JsonProperty("cardPoolType")]
        public string CardPoolType { get; set; }

        [JsonProperty("serverId")]
        public string ServerId { get; set; }

        [JsonProperty("languageCode")]
        public string LanguageCode { get; set; }

        [JsonProperty("recordId")]
        public string RecordId { get; set; }
    }
}
