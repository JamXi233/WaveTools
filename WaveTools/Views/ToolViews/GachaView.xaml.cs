using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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
using WaveTools.Depend;
using WaveTools.Depend;
using WaveTools.Views.GachaViews;
using Windows.Graphics;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;
using static WaveTools.App;
using System.Runtime.InteropServices;

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
        public static GachaModel.CardPoolInfo cardPoolInfo;

        private bool isUserInteraction = false;
        private string latestUpdatedUID = null;

        [DllImport("User32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        public GachaView()
        {
            InitializeComponent();
            Logging.Write("Switch to GachaView", 0);
            this.Loaded += GachaView_Loaded;
        }

        private async void GachaView_Loaded(object sender, RoutedEventArgs e)
        {
            if (AppDataController.GetGamePath() == "Null") { GetGachaURL.IsEnabled = false; UpdateGacha.IsEnabled = false; }
            // 获取卡池信息
            cardPoolInfo = await GetCardPoolInfo();

            if (cardPoolInfo == null || cardPoolInfo.CardPools == null)
            {
                Console.WriteLine("无法获取卡池信息或卡池列表为空");
                return;
            }
            await LoadUIDs();
        }

        private async Task<GachaModel.CardPoolInfo> GetCardPoolInfo()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var response = await client.GetStringAsync("https://wavetools.jamsg.cn/api/cardPoolRule");
                    return JsonConvert.DeserializeObject<GachaModel.CardPoolInfo>(response);
                }
            }
            catch (Exception ex)
            {
                NotificationManager.RaiseNotification("获取卡池信息时发生错误", null, InfoBarSeverity.Error, false, 5);
                Logging.Write($"获取卡池信息时发生错误: {ex.Message}",2);
                throw;
            }
        }

        private async void GetGachaURL_Click(object sender, RoutedEventArgs e)
        {
            string recordsBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"JSG-LLC\WaveTools\GachaRecords");
            string gachaLinksJson = await ProcessRun.WaveToolsHelperAsync($"/GetGachaURL {AppDataController.GetGamePathForHelper()}");
            try 
            {
                var gachaUrls = JsonConvert.DeserializeObject<List<GachaModel.GachaUrl>>(gachaLinksJson);

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
            catch { NotificationManager.RaiseNotification("获取抽卡记录失败", "可能是未打开过游戏\n或未打开抽卡记录", InfoBarSeverity.Warning, true, 2); }

        }

        private async void OpenGachaWeb_Click(object sender, RoutedEventArgs e)
        {
            WaitOverlayManager.RaiseWaitOverlay(true, "正在获取抽卡连接", "请耐心等待", true, 0);
            string gachaUrl = await ProcessRun.WaveToolsHelperAsync($"/GetSavedGachaURL {selectedUid}");
            if (!gachaUrl.Contains("https"))
            {
                NotificationManager.RaiseNotification("打开抽卡记录失败", "该抽卡记录可能是导入到工具箱的\n导入的抽卡记录无法打开抽卡记录 需获取本地抽卡记录才可打开", InfoBarSeverity.Warning);
                WaitOverlayManager.RaiseWaitOverlay(false); // 添加这行以确保在错误情况下取消等待覆盖
                return;
            }

            var newWindow = new Window();
            newWindow.Title = $"[UID:{selectedUid}]抽卡记录";
            WaitOverlayManager.RaiseWaitOverlay(true, "抽卡记录已在新窗口打开", null, false, 0 );

            var webView = new WebView2
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var grid = new Grid();
            grid.Children.Add(webView);

            newWindow.Content = grid;

            IntPtr hWnd = WindowNative.GetWindowHandle(newWindow);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            appWindow.Resize(new SizeInt32(1141, 641));
            newWindow.Closed += (s, args) =>
            {
                WaitOverlayManager.RaiseWaitOverlay(false);
            };

            newWindow.Activate();
            await webView.EnsureCoreWebView2Async(null);

            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.Source = new Uri(gachaUrl);
        }

        private async void UpdateGacha_Click(object sender, RoutedEventArgs e)
        {
            bool isShiftPressed = (GetAsyncKeyState(0x10) & 0x8000) != 0;
            if (isShiftPressed) DialogManager.RaiseDialog(XamlRoot, "抽卡记录遇到了问题?", $"可以尝试完全覆盖当前抽卡记录\n只有在抽卡记录完全混乱时才建议使用", true, "强制覆盖", () => { UpdateGacha_R(selectedUid,true); });
            else UpdateGacha_R(selectedUid);
        }

        private async void SaveGachaLink(string UID)
        {
            WaitOverlayManager.RaiseWaitOverlay(true, "正在保存抽卡链接", "请稍等片刻", true, 0);
            await ProcessRun.WaveToolsHelperAsync($"/SaveGachaURL {AppDataController.GetGamePathForHelper()} {UID}");
            WaitOverlayManager.RaiseWaitOverlay(false);
            await GetGachaRecords(UID);
            ReloadGachaView();
        }

        private async Task GetGachaRecords(string UID)
        {
            WaitOverlayManager.RaiseWaitOverlay(true, "正在获取抽卡记录", "请稍等片刻", true, 0);
            await ProcessRun.WaveToolsHelperAsync($"/GetGachaRecords {UID}");
            NotificationManager.RaiseNotification("获取完成",null,InfoBarSeverity.Success,false,2);
            WaitOverlayManager.RaiseWaitOverlay(false);
            ReloadGachaView();
        }

        private async void UpdateGacha_R(string UID, bool isForce = false)
        {
            if (isForce)
            {
                if (UID is null) { NotificationManager.RaiseNotification($"更新抽卡记录", "更新记录失败:UID为空", InfoBarSeverity.Error); return; }
                WaitOverlayManager.RaiseWaitOverlay(true, "正在完全覆盖抽卡记录", "请稍等片刻", true, 0);
                await ProcessRun.WaveToolsHelperAsync($"/UpdateGachaRecords {UID} /force");
                latestUpdatedUID = UID;
                WaitOverlayManager.RaiseWaitOverlay(false);
                NotificationManager.RaiseNotification("覆盖完成", null, InfoBarSeverity.Success, false, 1);
            }
            else
            {
                if (UID is null) { NotificationManager.RaiseNotification($"更新抽卡记录", "更新记录失败:UID为空", InfoBarSeverity.Error); return; }
                WaitOverlayManager.RaiseWaitOverlay(true, "正在更新抽卡记录", "请稍等片刻", true, 0);
                await ProcessRun.WaveToolsHelperAsync($"/UpdateGachaRecords {UID}");
                latestUpdatedUID = UID;
                WaitOverlayManager.RaiseWaitOverlay(false);
                NotificationManager.RaiseNotification("更新完成", null, InfoBarSeverity.Success, false, 1);
            }
            ReloadGachaView();
        }

        private async void ReloadGachaView(TeachingTip sender = null, object args = null)
        {
            GachaRecordsUID.SelectionChanged -= GachaRecordsUID_SelectionChanged;
            await LoadUIDs();
            GachaRecordsUID.SelectionChanged += GachaRecordsUID_SelectionChanged;

            ReloadGachaMainView();

            if (!string.IsNullOrEmpty(latestUpdatedUID))
            {
                isUserInteraction = false;
                GachaRecordsUID.SelectedItem = latestUpdatedUID;
                latestUpdatedUID = null;
            }
            isUserInteraction = true;
        }

        private void ComboBox_Click(object sender, object e)
        {
            isUserInteraction = true;
        }

        private void ReloadGachaComboBox()
        {
            selectedUid = null;
            GachaRecordsUID.SelectionChanged -= GachaRecordsUID_SelectionChanged;
            GachaRecordsUID.ItemsSource = null;
            GachaRecordsUID.Items.Clear();

            string linkDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JSG-LLC", "WaveTools", "GachaLinks");
            string recordsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JSG-LLC", "WaveTools", "GachaRecords");

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
            ReloadGachaComboBox();
            if (GachaRecordsUID.Items.Count == 0)
            {
                gachaView.Visibility = Visibility.Collapsed;
                loadGachaProgress.Visibility = Visibility.Collapsed;
                noGachaFound.Visibility = Visibility.Visible;
            }
            else
            {
                gachaView.Visibility = Visibility.Visible;
            }
            if (gachaFrame != null)
            {
                gachaFrame.Navigate(typeof(TempGachaView), selectedUid);
            }
            gachaNav.Items.Clear();
            if (!string.IsNullOrEmpty(selectedUid))
            {
                LoadGachaRecords(selectedUid);
            }
            ClearGacha.IsEnabled = !string.IsNullOrEmpty(selectedUid);
            OpenGachaWeb.IsEnabled = !string.IsNullOrEmpty(selectedUid);

            loadGachaProgress.Visibility = Visibility.Collapsed;
            noGachaFound.Visibility = Visibility.Collapsed;
        }

        private async Task LoadUIDs()
        {
            GachaRecordsUID.ItemsSource = null;
            try
            {
                string linkDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JSG-LLC", "WaveTools", "GachaLinks");
                string recordsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JSG-LLC", "WaveTools", "GachaRecords");

                HashSet<string> uidSet = new HashSet<string>();

                if (Directory.Exists(recordsDirectory))
                {
                    var recordFiles = Directory.GetFiles(recordsDirectory, "*.json");
                    foreach (var file in recordFiles)
                    {
                        uidSet.Add(Path.GetFileNameWithoutExtension(file));
                    }
                }

                if (Directory.Exists(linkDirectory))
                {
                    var linkFiles = Directory.GetFiles(linkDirectory, "*.json");
                    foreach (var file in linkFiles)
                    {
                        uidSet.Add(Path.GetFileNameWithoutExtension(file));
                    }
                }

                if (uidSet.Count == 0)
                {
                    //NotificationManager.RaiseNotification($"抽卡分析", "无抽卡记录", InfoBarSeverity.Warning);
                    loadGachaProgress.Visibility = Visibility.Collapsed;
                    noGachaFound.Visibility = Visibility.Visible;
                    ExportWWGF.IsEnabled = false;
                    return;
                }

                GachaRecordsUID.ItemsSource = uidSet.ToList();
                if (uidSet.Count > 0)
                {
                    GachaRecordsUID.SelectedIndex = 0;
                    loadGachaProgress.Visibility = Visibility.Collapsed;
                    noGachaFound.Visibility = Visibility.Collapsed;
                    gachaView.Visibility = Visibility.Visible;
                    ClearGacha.IsEnabled = true;
                    OpenGachaWeb.IsEnabled = true;
                    ExportWWGF.IsEnabled = true;
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
                        LoadGachaRecords(selectedUid);
                    }
                    else
                    {
                        Logging.Write("GachaUID:" + selectedUid);
                        LoadGachaRecords(selectedUid);
                        isUserInteraction = true;
                    }
                }
                else
                {
                    Logging.Write("UID为空");
                }
            }
        }

        private async void LoadGachaRecords(string uid)
        {
            try
            {
                Logging.Write("Load GachaRecords...");
                string recordsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JSG-LLC", "WaveTools", "GachaRecords");
                string filePath = Path.Combine(recordsDirectory, $"{uid}.json");

                if (!File.Exists(filePath))
                {
                    if (isUserInteraction)
                    {
                        Logging.Write("GachaRecord Need Update",1);
                        DialogManager.RaiseDialog(XamlRoot, "抽卡记录未找到", $"未找到UID:{uid}的抽卡记录文件\n需要更新抽卡记录", true, "更新", () => { UpdateGacha_R(uid); });
                        gachaNav.Visibility = Visibility.Collapsed;
                        gachaFrame.Visibility = Visibility.Collapsed;
                        return;
                    }

                    // 使用for循环遍历所有选项
                    bool recordFound = false;
                    isUserInteraction = false;
                    GachaRecordsUID.SelectionChanged -= GachaRecordsUID_SelectionChanged;
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
                        GachaRecordsUID.SelectedIndex = 0;
                        NotificationManager.RaiseNotification("无可用的抽卡记录", "", InfoBarSeverity.Warning);
                        gachaNav.Visibility = Visibility.Collapsed;
                        gachaFrame.Visibility = Visibility.Collapsed;
                    }
                    GachaRecordsUID.SelectionChanged += GachaRecordsUID_SelectionChanged;
                    return;
                }

                var jsonContent = await File.ReadAllTextAsync(filePath);
                var gachaData = JsonConvert.DeserializeObject<GachaModel.GachaData>(jsonContent);

                DisplayGachaData(gachaData);
            }
            catch (Exception ex)
            {
                NotificationManager.RaiseNotification("加载抽卡记录时发生错误", $"{ex.Message}", InfoBarSeverity.Error);
            }
        }

        private void DisplayGachaData(GachaModel.GachaData gachaData)
        {
            Logging.Write("Display GachaData...");
            gachaNav.Items.Clear();

            if (gachaData?.List == null || gachaData.List.Count == 0)
            {
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

                item.Tapped += SelectorBarItem_Tapped;
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

        private void SelectorBarItem_Tapped(object sender, TappedRoutedEventArgs e)
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
                gachaFrame.Navigate(typeof(TempGachaView), selectedUid);
            }
        }


        private async void ExportWWGF_Click(object sender, RoutedEventArgs e)
        {
            var window = new Window();
            string recordsBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"JSG-LLC\WaveTools\GachaRecords");

            // 打开文件选择器
            var savePicker = new FileSavePicker();
            var hwnd = WindowNative.GetWindowHandle(window);
            InitializeWithWindow.Initialize(savePicker, hwnd);

            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("Wuthering Waves Gacha Format", new List<string>() { ".json" });
            savePicker.SuggestedFileName = $"{selectedUid}_Export";

            StorageFile exportFile = await savePicker.PickSaveFileAsync();
            if (exportFile != null)
            {
                ExportGacha.Export($"{recordsBasePath}\\{selectedUid}.json", exportFile.Path);
            }
        }

        private async void ImportWWGF_Click(object sender, RoutedEventArgs e)
        {
            var window = new Window();
            // 打开文件选择器
            var openPicker = new FileOpenPicker();
            var hwnd = WindowNative.GetWindowHandle(window);
            InitializeWithWindow.Initialize(openPicker, hwnd);

            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add(".json");

            StorageFile importFile = await openPicker.PickSingleFileAsync();
            if (importFile != null)
            {
                string recordsBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"JSG-LLC\WaveTools\GachaRecords");
                await ImportGacha.Import(importFile.Path);
            }
            ReloadGachaView();
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

}
