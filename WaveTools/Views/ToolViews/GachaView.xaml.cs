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

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Vanara.PInvoke;
using WaveTools.Depend;
using WaveTools.Views.GachaViews;
using Windows.Graphics;
using WinRT.Interop;
using static WaveTools.App;

namespace WaveTools.Views.ToolViews
{
    public sealed partial class GachaView : Page
    {
        public bool isProxyRunning;
        public static string selectedUid;
        public static int selectedCardPoolId;
        public string GachaLink_String;
        public string GachaLinkCache_String;
        public bool isClearGachaSaved;
        public static GachaModel.CardPoolInfo cardPoolInfo;

        private bool isUserInteraction = false;
        private string latestUpdatedUID = null;

        private bool isRefreshingGachaRecords = false;
        private bool isChangingGachaUidProgrammatically = false;
        private int gachaRecordsLoadToken = 0;

        private bool _hasLoadedOnce;
        private bool _isRefreshingTempGachaViewByHotkey;

        [DllImport("User32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        public GachaView()
        {
            InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            Logging.Write("Switch to GachaView", 0);
            this.Loaded += GachaView_Loaded;

            KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;

            var refreshGachaAccelerator = new KeyboardAccelerator
            {
                Key = Windows.System.VirtualKey.F5
            };

            refreshGachaAccelerator.Invoked += RefreshGachaAccelerator_Invoked;
            KeyboardAccelerators.Add(refreshGachaAccelerator);
        }

        private async void GachaView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_hasLoadedOnce)
            {
                return;
            }

            _hasLoadedOnce = true;

            if (AppDataController.GetGamePath() == "Null")
            {
                GetGachaURL.IsEnabled = false;
                UpdateGacha.IsEnabled = false;
            }

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
                Logging.Write($"获取卡池信息时发生错误: {ex.Message}", 2);
                throw;
            }
        }

        private async void GetGachaURL_Click(object sender, RoutedEventArgs e)
        {
            string recordsBasePath = AppDataController.GetDataPath("GachaRecords");
            string gachaLinksJson = await ProcessRun.WaveToolsHelperAsync($"/GetGachaURL {AppDataController.GetGamePathForHelper()}");

            try
            {
                var gachaUrls = JsonConvert.DeserializeObject<List<GachaModel.GachaUrl>>(gachaLinksJson);

                var dialog = new ContentDialog
                {
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                    Title = "选择UID",
                    PrimaryButtonText = "确认",
                    SecondaryButtonText = "复制URL",
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

                stackPanel.Children.Add(new TextBlock
                {
                    Text = "注：未显示已保存的UID\n再次保存[已存在]的UID来更新抽卡链接",
                    TextAlignment = TextAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 8)
                });

                var comboBox = new ComboBox
                {
                    Width = 300,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                if (gachaUrls == null || gachaUrls.Count == 0)
                {
                    var noFound = new TextBlock
                    {
                        Text = "未找到新的抽卡记录\n请到游戏内打开一次抽卡记录",
                        TextAlignment = TextAlignment.Left
                    };

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
                        else
                        {
                            items.Add(url.PlayerId + "[已存在]");
                        }
                    }

                    if (items.Count == 0)
                    {
                        var noFound = new TextBlock
                        {
                            Text = "未找到新的抽卡记录",
                            TextAlignment = TextAlignment.Left
                        };

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

                    if (selectedUid.Contains("[已存在]"))
                    {
                        selectedUid = selectedUid.Replace("[已存在]", "").Trim();
                    }

                    SaveGachaLink(selectedUid);
                }
                else if (result == ContentDialogResult.Secondary && comboBox.SelectedItem != null && comboBox.IsEnabled)
                {
                    string selectedUid = comboBox.SelectedItem as string;

                    if (selectedUid.Contains("[已存在]"))
                    {
                        selectedUid = selectedUid.Replace("[已存在]", "").Trim();
                    }

                    var selectedGachaUrl = gachaUrls.FirstOrDefault(url => url.PlayerId == selectedUid)?.GachaLink;

                    if (!string.IsNullOrEmpty(selectedGachaUrl))
                    {
                        System.Windows.Clipboard.SetText(selectedGachaUrl);
                        NotificationManager.RaiseNotification("复制成功", "抽卡记录URL已复制完成", InfoBarSeverity.Success, true, 2);
                    }
                }
            }
            catch
            {
                NotificationManager.RaiseNotification("获取抽卡记录失败", "可能是未打开过游戏\n或未打开抽卡记录", InfoBarSeverity.Warning, true, 2);
            }
        }

        private async void OpenGachaWeb_Click(object sender, RoutedEventArgs e)
        {
            WaitOverlayManager.RaiseWaitOverlay(true, "正在获取抽卡连接", "请耐心等待", true, 0);

            string gachaUrl = await ProcessRun.WaveToolsHelperAsync($"/GetSavedGachaURL {selectedUid}");

            if (!gachaUrl.Contains("https"))
            {
                NotificationManager.RaiseNotification("打开抽卡记录失败", "该抽卡记录可能是导入到工具箱的\n导入的抽卡记录无法打开抽卡记录 需获取本地抽卡记录才可打开", InfoBarSeverity.Warning);
                WaitOverlayManager.RaiseWaitOverlay(false);
                return;
            }

            var newWindow = new Window();
            newWindow.Title = $"[UID:{selectedUid}]抽卡记录";
            WaitOverlayManager.RaiseWaitOverlay(true, "抽卡记录已在新窗口打开", null, false, 0);

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
            string uidToUpdate = selectedUid;

            if (string.IsNullOrWhiteSpace(uidToUpdate))
            {
                uidToUpdate = GetSelectedUidFromComboBox();
            }

            if (string.IsNullOrWhiteSpace(uidToUpdate))
            {
                NotificationManager.RaiseNotification(
                    "未选择UID",
                    "请先选择需要更新的UID",
                    InfoBarSeverity.Warning,
                    false,
                    3
                );
                return;
            }

            int preferredCardPoolId = selectedCardPoolId;

            try
            {
                isRefreshingGachaRecords = true;

                UpdateGacha.IsEnabled = false;
                GetGachaURL.IsEnabled = false;
                ClearGacha.IsEnabled = false;
                GachaRecordsUID.IsEnabled = false;
                GachaPoolComboBox.IsEnabled = false;

                gacha_status.Text = "正在更新抽卡记录...";

                string result = await ProcessRun.WaveToolsHelperAsync($"/UpdateGachaRecords {uidToUpdate}");

                Logging.Write(result);

                bool updateSuccess = HandleGachaRecordsResult(result, "更新完成", uidToUpdate);

                selectedUid = uidToUpdate;
                selectedCardPoolId = preferredCardPoolId;

                if (!updateSuccess)
                {
                    gacha_status.Text = "抽卡记录更新失败";
                    await LoadGachaRecords(uidToUpdate, preferredCardPoolId);
                    return;
                }

                gacha_status.Text = "抽卡记录更新完成，正在刷新...";

                await LoadGachaRecords(uidToUpdate, preferredCardPoolId);

                gacha_status.Text = "抽卡记录刷新完成";
            }
            catch (Exception ex)
            {
                Logging.Write($"UpdateGacha_Click failed: {ex.Message}", 1);

                gacha_status.Text = "抽卡记录更新失败";

                NotificationManager.RaiseNotification(
                    "更新抽卡记录失败",
                    ex.Message,
                    InfoBarSeverity.Error,
                    false,
                    8
                );

                selectedUid = uidToUpdate;
                selectedCardPoolId = preferredCardPoolId;

                await LoadGachaRecords(uidToUpdate, preferredCardPoolId);
            }
            finally
            {
                isRefreshingGachaRecords = false;

                UpdateGacha.IsEnabled = true;
                GetGachaURL.IsEnabled = true;
                ClearGacha.IsEnabled = !string.IsNullOrWhiteSpace(selectedUid);
                GachaRecordsUID.IsEnabled = true;
                GachaPoolComboBox.IsEnabled = GachaPoolComboBox.Items.Count > 0;
            }
        }

        private async void InputUrlUpdate_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                Title = "输入抽卡记录URL",
                PrimaryButtonText = "确认",
                CloseButtonText = "取消",
                XamlRoot = XamlRoot,
                DefaultButton = ContentDialogButton.Primary
            };

            var stackPanel = new StackPanel { Spacing = 8 };
            stackPanel.Children.Add(new TextBlock { Text = "请在此粘贴完整的抽卡记录URL：" });
            var textBox = new TextBox { AcceptsReturn = true, Height = 100, TextWrapping = TextWrapping.Wrap };
            stackPanel.Children.Add(textBox);

            dialog.Content = stackPanel;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string url = textBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(url))
                {
                    NotificationManager.RaiseNotification("无效的URL", "URL不能为空", InfoBarSeverity.Warning);
                    return;
                }

                if (!url.StartsWith("http") || (!url.Contains("resources_id") && !url.Contains("player_id")))
                {
                    NotificationManager.RaiseNotification("无效的URL", "请检查输入的URL是否正确", InfoBarSeverity.Warning);
                    return;
                }

                try
                {
                    string playerId = ExtractQueryParameter(url, "player_id");
                    if (string.IsNullOrEmpty(playerId))
                    {
                        NotificationManager.RaiseNotification("无效的URL", "未在URL中找到player_id", InfoBarSeverity.Warning);
                        return;
                    }

                    var gachaUrl = new GachaModel.GachaUrl
                    {
                        GachaLink = url,
                        PlayerId = playerId,
                        CardPoolType = ExtractQueryParameter(url, "gacha_type"),
                        RecordId = ExtractQueryParameter(url, "record_id"),
                        ServerId = ExtractQueryParameter(url, "svr_id"),
                        LanguageCode = ExtractQueryParameter(url, "lang")
                    };

                    string linkDirectory = AppDataController.GetDataPath("GachaLinks");
                    if (!Directory.Exists(linkDirectory))
                    {
                        Directory.CreateDirectory(linkDirectory);
                    }

                    string filePath = Path.Combine(linkDirectory, $"{playerId}.json");
                    // Wrap in a list to match Helper's expected format: List<Dictionary<string, string>>
                    string json = JsonConvert.SerializeObject(new List<GachaModel.GachaUrl> { gachaUrl });
                    File.WriteAllText(filePath, json);

                    NotificationManager.RaiseNotification("保存成功", $"UID:{playerId} 的URL已保存，正在获取记录...", InfoBarSeverity.Success);

                    latestUpdatedUID = playerId;

                    await GetGachaRecords(playerId);
                }
                catch (Exception ex)
                {
                    NotificationManager.RaiseNotification("解析URL失败", ex.Message, InfoBarSeverity.Error);
                }
            }
        }

        private string ExtractQueryParameter(string url, string paramName)
        {
            try
            {
                var uri = new Uri(url);
                string query = uri.Query;
                if (string.IsNullOrEmpty(query) && uri.AbsoluteUri.Contains("?"))
                {
                    query = uri.AbsoluteUri.Substring(uri.AbsoluteUri.IndexOf('?'));
                }
                
                if (string.IsNullOrEmpty(query)) return null;
                
                var args = query.TrimStart('?').Split('&');
                foreach (var arg in args)
                {
                    var parts = arg.Split('=', 2);
                    if (parts.Length == 2 && parts[0] == paramName)
                    {
                        return Uri.UnescapeDataString(parts[1]);
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }
            return null;
        }

        private string GetSelectedUidFromComboBox()
        {
            if (GachaRecordsUID == null)
            {
                return string.Empty;
            }

            if (GachaRecordsUID.SelectedItem is ComboBoxItem selectedItem)
            {
                return selectedItem.Content?.ToString() ?? string.Empty;
            }

            return GachaRecordsUID.SelectedItem?.ToString() ?? string.Empty;
        }

        private async void SaveGachaLink(string UID)
        {
            WaitOverlayManager.RaiseWaitOverlay(true, "正在保存抽卡链接", "请稍等片刻", true, 0);
            await ProcessRun.WaveToolsHelperAsync($"/SaveGachaURL {AppDataController.GetGamePathForHelper()} {UID}");
            await GetGachaRecords(UID);
            ReloadGachaView();
        }

        private async Task GetGachaRecords(string UID)
        {
            WaitOverlayManager.RaiseWaitOverlay(true, "正在获取抽卡记录", "请稍等片刻", true, 0);

            try
            {
                var result = await ProcessRun.WaveToolsHelperAsync($"/GetGachaRecords {UID}");

                Logging.Write(result);

                HandleGachaRecordsResult(result, "获取完成", UID);
            }
            catch (Exception ex)
            {
                Logging.Write($"GetGachaRecords failed: {ex.Message}", 1);

                NotificationManager.RaiseNotification(
                    "获取抽卡记录失败",
                    ex.Message,
                    InfoBarSeverity.Error,
                    false,
                    5
                );
            }
            finally
            {
                WaitOverlayManager.RaiseWaitOverlay(false);
                ReloadGachaView();
            }
        }

        private bool HandleGachaRecordsResult(string result, string successTitle, string uid = null)
        {
            if (string.IsNullOrWhiteSpace(result))
            {
                NotificationManager.RaiseNotification(
                    "操作失败",
                    "Helper 没有返回任何结果",
                    InfoBarSeverity.Error,
                    false,
                    5
                );

                return false;
            }

            if (result.Contains("抽卡链接已经失效") || result.Contains("失效"))
            {
                NotificationManager.RaiseNotification(
                    "抽卡链接已经失效",
                    "需要重新获取抽卡记录。",
                    InfoBarSeverity.Error,
                    false,
                    2
                );

                return false;
            }

            if (result.Contains("未找到保存的UID数据文件"))
            {
                NotificationManager.RaiseNotification(
                    "未找到抽卡记录URL",
                    "该记录可能为外部导入的记录，或还没有保存抽卡URL。",
                    InfoBarSeverity.Error,
                    false,
                    2
                );

                return false;
            }

            if (result.Contains("UID数据文件为空") ||
                result.Contains("UID数据文件内容为空或格式错误"))
            {
                NotificationManager.RaiseNotification(
                    "抽卡URL数据无效",
                    "请重新获取抽卡记录URL。",
                    InfoBarSeverity.Error,
                    false,
                    2
                );

                return false;
            }

            if (result.Contains("抽卡URL缺少 resources_id"))
            {
                NotificationManager.RaiseNotification(
                    "抽卡URL缺少必要参数",
                    "请重新获取抽卡记录URL。",
                    InfoBarSeverity.Error,
                    false,
                    2
                );

                return false;
            }

            if (result.Contains("发生错误") ||
                result.Contains("发生异常") ||
                result.Contains("失败"))
            {
                NotificationManager.RaiseNotification(
                    "操作失败",
                    result.Trim(),
                    InfoBarSeverity.Error,
                    false,
                    5
                );

                return false;
            }

            NotificationManager.RaiseNotification(
                successTitle,
                uid == null ? null : $"UID:{uid}",
                InfoBarSeverity.Success,
                false,
                2
            );

            return true;
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

            GachaPoolComboBox.SelectionChanged -= GachaPoolComboBox_SelectionChanged;
            GachaPoolComboBox.Items.Clear();
            GachaPoolComboBox.SelectedItem = null;
            GachaPoolComboBox.IsEnabled = false;
            GachaPoolComboBox.SelectionChanged += GachaPoolComboBox_SelectionChanged;

            string recordsDirectory = AppDataController.GetDataPath("GachaRecords");

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

            GachaRecordsUID.SelectionChanged += GachaRecordsUID_SelectionChanged;
        }

        private void ReloadGachaMainView()
        {
            isUserInteraction = false;

            ReloadGachaComboBox();

            GachaPoolComboBox.SelectionChanged -= GachaPoolComboBox_SelectionChanged;
            GachaPoolComboBox.Items.Clear();
            GachaPoolComboBox.SelectedItem = null;
            GachaPoolComboBox.IsEnabled = false;
            GachaPoolComboBox.SelectionChanged += GachaPoolComboBox_SelectionChanged;

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
                gachaFrame.Visibility = Visibility.Collapsed;
            }

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

            GachaPoolComboBox.SelectionChanged -= GachaPoolComboBox_SelectionChanged;
            GachaPoolComboBox.Items.Clear();
            GachaPoolComboBox.SelectedItem = null;
            GachaPoolComboBox.IsEnabled = false;
            GachaPoolComboBox.SelectionChanged += GachaPoolComboBox_SelectionChanged;

            try
            {
                string linkDirectory = AppDataController.GetDataPath("GachaLinks");
                string recordsDirectory = AppDataController.GetDataPath("GachaRecords");

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
                Logging.Write($"加载UID时出现错误: {ex.Message}", 2);
            }
        }

        private async void GachaRecordsUID_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isChangingGachaUidProgrammatically || isRefreshingGachaRecords)
            {
                Logging.Write("GachaRecordsUID_SelectionChanged ignored because gacha records are refreshing");
                return;
            }

            string uid = GetSelectedUidFromComboBox();

            if (string.IsNullOrWhiteSpace(uid))
            {
                return;
            }

            bool isDifferentUid = !string.Equals(selectedUid, uid, StringComparison.Ordinal);

            selectedUid = uid;
            if (isDifferentUid)
            {
                selectedCardPoolId = 0;

                GachaPoolComboBox.SelectionChanged -= GachaPoolComboBox_SelectionChanged;
                GachaPoolComboBox.Items.Clear();
                GachaPoolComboBox.SelectedItem = null;
                GachaPoolComboBox.IsEnabled = false;
                GachaPoolComboBox.Width = 120;
                GachaPoolComboBox.SelectionChanged += GachaPoolComboBox_SelectionChanged;

                if (gachaFrame != null)
                {
                    gachaFrame.Visibility = Visibility.Collapsed;
                }
            }

            Logging.Write($"GachaUID:{selectedUid}");
            await LoadGachaRecords(selectedUid);
        }

        private async Task LoadGachaRecords(string uid = null, int? preferredCardPoolId = null)
        {
            int currentLoadToken = ++gachaRecordsLoadToken;

            Logging.Write("Load GachaRecords...");

            if (string.IsNullOrWhiteSpace(uid))
            {
                uid = selectedUid;
            }

            if (string.IsNullOrWhiteSpace(uid))
            {
                loadGachaProgress.Visibility = Visibility.Collapsed;
                noGachaFound.Visibility = Visibility.Visible;
                gachaView.Visibility = Visibility.Collapsed;
                return;
            }

            selectedUid = uid;

            loadGachaProgress.Visibility = Visibility.Visible;
            noGachaFound.Visibility = Visibility.Collapsed;
            gachaView.Visibility = Visibility.Collapsed;

            GachaPoolComboBox.SelectionChanged -= GachaPoolComboBox_SelectionChanged;
            GachaPoolComboBox.Items.Clear();
            GachaPoolComboBox.SelectedItem = null;
            GachaPoolComboBox.IsEnabled = false;
            GachaPoolComboBox.Width = 120;
            GachaPoolComboBox.SelectionChanged += GachaPoolComboBox_SelectionChanged;

            string recordsDirectory = AppDataController.GetDataPath("GachaRecords");

            string recordsFilePath = Path.Combine(recordsDirectory, $"{uid}.json");

            if (!File.Exists(recordsFilePath))
            {
                if (currentLoadToken != gachaRecordsLoadToken)
                {
                    return;
                }

                loadGachaProgress.Visibility = Visibility.Collapsed;
                noGachaFound.Visibility = Visibility.Visible;
                gachaView.Visibility = Visibility.Collapsed;

                return;
            }

            string jsonContent;

            try
            {
                jsonContent = await File.ReadAllTextAsync(recordsFilePath);
            }
            catch (Exception ex)
            {
                Logging.Write($"Read gacha records failed: {ex.Message}", 1);

                if (currentLoadToken != gachaRecordsLoadToken)
                {
                    return;
                }

                loadGachaProgress.Visibility = Visibility.Collapsed;
                noGachaFound.Visibility = Visibility.Visible;
                gachaView.Visibility = Visibility.Collapsed;

                return;
            }

            if (currentLoadToken != gachaRecordsLoadToken)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                loadGachaProgress.Visibility = Visibility.Collapsed;
                noGachaFound.Visibility = Visibility.Visible;
                gachaView.Visibility = Visibility.Collapsed;

                return;
            }

            GachaModel.GachaData gachaData;

            try
            {
                gachaData = JsonConvert.DeserializeObject<GachaModel.GachaData>(jsonContent);
            }
            catch (Exception ex)
            {
                Logging.Write($"Deserialize gacha records failed: {ex.Message}", 1);

                loadGachaProgress.Visibility = Visibility.Collapsed;
                noGachaFound.Visibility = Visibility.Visible;
                gachaView.Visibility = Visibility.Collapsed;

                return;
            }

            if (currentLoadToken != gachaRecordsLoadToken)
            {
                return;
            }

            DisplayGachaData(gachaData, preferredCardPoolId);
        }

        private void DisplayGachaData(GachaModel.GachaData gachaData, int? preferredCardPoolId = null)
        {
            Logging.Write("Display GachaData...");

            GachaPoolComboBox.SelectionChanged -= GachaPoolComboBox_SelectionChanged;

            try
            {
                GachaPoolComboBox.Items.Clear();
                GachaPoolComboBox.SelectedItem = null;
                GachaPoolComboBox.IsEnabled = false;
                GachaPoolComboBox.Width = 120;

                if (gachaFrame != null)
                {
                    gachaFrame.Visibility = Visibility.Collapsed;
                }

                if (gachaData == null || gachaData.List == null || gachaData.List.Count == 0)
                {
                    loadGachaProgress.Visibility = Visibility.Collapsed;
                    noGachaFound.Visibility = Visibility.Visible;
                    gachaView.Visibility = Visibility.Collapsed;
                    return;
                }

                var availablePools = gachaData.List
                    .Where(pool => pool.Records != null && pool.Records.Count > 0)
                    .OrderBy(pool => pool.CardPoolId)
                    .ToList();

                Logging.Write($"Available gacha pools count: {availablePools.Count}");

                if (availablePools.Count == 0)
                {
                    loadGachaProgress.Visibility = Visibility.Collapsed;
                    noGachaFound.Visibility = Visibility.Visible;
                    gachaView.Visibility = Visibility.Collapsed;
                    return;
                }

                foreach (var pool in availablePools)
                {
                    Logging.Write($"Available pool: cardPoolId={pool.CardPoolId}, cardPoolType={pool.CardPoolType}, records={pool.Records.Count}");

                    var item = new ComboBoxItem
                    {
                        Content = pool.CardPoolType,
                        Tag = pool.CardPoolId.ToString(),
                        IsEnabled = true
                    };

                    GachaPoolComboBox.Items.Add(item);
                }

                ComboBoxItem targetItem = null;

                if (preferredCardPoolId.HasValue)
                {
                    foreach (var comboBoxObject in GachaPoolComboBox.Items)
                    {
                        if (comboBoxObject is ComboBoxItem item &&
                            item.Tag != null &&
                            int.TryParse(item.Tag.ToString(), out int itemCardPoolId) &&
                            itemCardPoolId == preferredCardPoolId.Value)
                        {
                            targetItem = item;
                            break;
                        }
                    }
                }

                if (targetItem == null && selectedCardPoolId > 0)
                {
                    foreach (var comboBoxObject in GachaPoolComboBox.Items)
                    {
                        if (comboBoxObject is ComboBoxItem item &&
                            item.Tag != null &&
                            int.TryParse(item.Tag.ToString(), out int itemCardPoolId) &&
                            itemCardPoolId == selectedCardPoolId)
                        {
                            targetItem = item;
                            break;
                        }
                    }
                }

                if (targetItem == null && GachaPoolComboBox.Items.Count > 0)
                {
                    targetItem = GachaPoolComboBox.Items[0] as ComboBoxItem;
                }

                if (targetItem == null || targetItem.Tag == null)
                {
                    loadGachaProgress.Visibility = Visibility.Collapsed;
                    noGachaFound.Visibility = Visibility.Visible;
                    gachaView.Visibility = Visibility.Collapsed;
                    return;
                }

                GachaPoolComboBox.IsEnabled = true;
                GachaPoolComboBox.SelectedItem = targetItem;

                selectedCardPoolId = int.Parse(targetItem.Tag.ToString());

                UpdateGachaPoolComboBoxWidth(targetItem.Content?.ToString());

                loadGachaProgress.Visibility = Visibility.Collapsed;
                noGachaFound.Visibility = Visibility.Collapsed;
                gachaView.Visibility = Visibility.Visible;

                if (gachaFrame != null)
                {
                    gachaFrame.Visibility = Visibility.Visible;
                }

                LoadGachaPoolData(targetItem.Tag.ToString());
            }
            finally
            {
                GachaPoolComboBox.SelectionChanged += GachaPoolComboBox_SelectionChanged;
            }
        }

        private void GachaPoolComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GachaPoolComboBox.SelectedItem is not ComboBoxItem item)
            {
                return;
            }

            if (item.Tag == null)
            {
                return;
            }

            string cardPoolId = item.Tag.ToString();

            if (string.IsNullOrWhiteSpace(cardPoolId))
            {
                return;
            }

            string cardPoolName = item.Content?.ToString();

            Logging.Write($"Selected Card Pool: {cardPoolId}");

            UpdateGachaPoolComboBoxWidth(cardPoolName);

            LoadGachaPoolData(cardPoolId);
        }

        private async void RefreshGachaAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            await RefreshTempGachaViewAsync();
        }

        private async Task RefreshTempGachaViewAsync()
        {
            if (_isRefreshingTempGachaViewByHotkey)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedUid) || selectedCardPoolId <= 0)
            {
                return;
            }

            if (gachaFrame == null || gachaView.Visibility != Visibility.Visible)
            {
                return;
            }

            _isRefreshingTempGachaViewByHotkey = true;

            try
            {
                Logging.Write("Refresh TempGachaView by F5", 0);

                if (gachaFrame.Content is TempGachaView tempGachaView)
                {
                    await tempGachaView.RefreshDataAsync();
                }
                else
                {
                    LoadGachaPoolData(selectedCardPoolId.ToString());
                }
            }
            catch (Exception ex)
            {
                Logging.Write($"Refresh TempGachaView failed: {ex.Message}", 2);
                NotificationManager.RaiseNotification("抽卡分析刷新失败", ex.Message, InfoBarSeverity.Error, false, 5);
            }
            finally
            {
                _isRefreshingTempGachaViewByHotkey = false;
            }
        }

        private void UpdateGachaPoolComboBoxWidth(string text)
        {
            const double minWidth = 112;
            const double maxWidth = 290;
            const double basePadding = 42;
            const double chineseCharWidth = 14.8;
            const double normalCharWidth = 7.5;

            if (string.IsNullOrWhiteSpace(text))
            {
                GachaPoolComboBox.Width = minWidth;
                return;
            }

            double calculatedWidth = basePadding;

            foreach (char c in text)
            {
                calculatedWidth += c > 127 ? chineseCharWidth : normalCharWidth;
            }

            calculatedWidth += 8;

            if (calculatedWidth < minWidth)
            {
                calculatedWidth = minWidth;
            }

            if (calculatedWidth > maxWidth)
            {
                calculatedWidth = maxWidth;
            }

            GachaPoolComboBox.Width = calculatedWidth;
        }

        private void LoadGachaPoolData(string cardPoolId)
        {
            if (string.IsNullOrWhiteSpace(cardPoolId))
            {
                return;
            }

            if (!int.TryParse(cardPoolId, out int parsedCardPoolId))
            {
                Logging.Write($"Invalid Card Pool ID: {cardPoolId}", 1);
                return;
            }

            selectedCardPoolId = parsedCardPoolId;

            if (string.IsNullOrWhiteSpace(selectedUid))
            {
                selectedUid = GetSelectedUidFromComboBox();
            }

            if (string.IsNullOrWhiteSpace(selectedUid))
            {
                Logging.Write("LoadGachaPoolData failed because selectedUid is empty", 1);
                return;
            }

            if (gachaFrame != null)
            {
                gachaFrame.Visibility = Visibility.Visible;
                gachaFrame.Navigate(typeof(TempGachaView), selectedUid);
            }
        }

        private async void ExportWWGF_Click(object sender, RoutedEventArgs e)
        {
            string recordsBasePath = AppDataController.GetDataPath("GachaRecords");
            DateTime now = DateTime.Now;
            string formattedDate = now.ToString("yyyy_MM_dd_HH_mm_ss");

            var suggestFileName = $"WaveTools_Gacha_Export_{selectedUid}_{formattedDate}";

            var fileTypeChoices = new Dictionary<string, List<string>>
            {
                { "Wuthering Waves Gacha Format", new List<string> { ".json" } }
            };

            var defaultExtension = ".json";

            string filePath = await CommonHelpers.FileHelpers.SaveFile(suggestFileName, fileTypeChoices, defaultExtension);

            if (filePath != null)
            {
                ExportGacha.Export($"{recordsBasePath}\\{selectedUid}.json", filePath);
            }
        }

        private async void ImportWWGF_Click(object sender, RoutedEventArgs e)
        {
            string filePath = await CommonHelpers.FileHelpers.OpenFile(".json");

            if (filePath != null)
            {
                string recordsBasePath = AppDataController.GetDataPath("GachaRecords");
                await ImportGacha.Import(filePath);
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
                BorderBrush = (Brush)Application.Current.Resources["AppSeparatorBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };
        }

        private void gachaFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            StretchGachaFrameContent();
        }

        private void gachaFrame_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            StretchGachaFrameContent();
        }

        private void StretchGachaFrameContent()
        {
            if (gachaFrame.Content is FrameworkElement content)
            {
                content.HorizontalAlignment = HorizontalAlignment.Stretch;
                content.VerticalAlignment = VerticalAlignment.Stretch;
                content.Width = gachaFrame.ActualWidth;
                content.Height = gachaFrame.ActualHeight;
            }
        }

        public async void CreateCapture_Click(object sender, RoutedEventArgs e)
        {
            var content = new StackPanel
            {
                Spacing = 4
            };

            var checkBoxScreenShotSelf = new CheckBox
            {
                Content = "是否自动截图",
                IsChecked = true
            };

            var checkBoxShowRecords = new CheckBox
            {
                Content = "是否显示抽卡记录",
                IsChecked = true
            };

            var checkBoxHideUID = new CheckBox
            {
                Content = "是否隐藏UID",
                IsChecked = true
            };

            var checkBoxShowAllGachaInfo = new CheckBox
            {
                Content = "显示全部抽卡详情",
                IsChecked = false
            };

            var gachaInfoDisplayCountTextBox = new TextBox
            {
                Text = "12",
                Width = 80,
                PlaceholderText = "12"
            };

            gachaInfoDisplayCountTextBox.TextChanged += (s, args) =>
            {
                string oldText = gachaInfoDisplayCountTextBox.Text;
                string newText = new string(oldText.Where(char.IsDigit).ToArray());

                if (oldText != newText)
                {
                    gachaInfoDisplayCountTextBox.Text = newText;
                    gachaInfoDisplayCountTextBox.SelectionStart = gachaInfoDisplayCountTextBox.Text.Length;
                }
            };

            checkBoxShowAllGachaInfo.Checked += (s, args) =>
            {
                gachaInfoDisplayCountTextBox.IsEnabled = false;
            };

            checkBoxShowAllGachaInfo.Unchecked += (s, args) =>
            {
                gachaInfoDisplayCountTextBox.IsEnabled = true;
            };

            var gachaInfoDisplayCountPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };

            gachaInfoDisplayCountPanel.Children.Add(new TextBlock
            {
                Text = "显示",
                VerticalAlignment = VerticalAlignment.Center
            });

            gachaInfoDisplayCountPanel.Children.Add(gachaInfoDisplayCountTextBox);

            gachaInfoDisplayCountPanel.Children.Add(new TextBlock
            {
                Text = "个抽卡详情",
                VerticalAlignment = VerticalAlignment.Center
            });

            content.Children.Add(new TextBlock
            {
                Text = "通用设置",
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold
            });

            content.Children.Add(checkBoxScreenShotSelf);

            content.Children.Add(new StackPanel
            {
                Height = 1,
                Background = (Brush)Application.Current.Resources["AppSeparatorBrush"]
            });

            content.Children.Add(new TextBlock
            {
                Text = "截图设置",
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold
            });

            content.Children.Add(checkBoxShowRecords);
            content.Children.Add(checkBoxHideUID);
            content.Children.Add(gachaInfoDisplayCountPanel);
            content.Children.Add(checkBoxShowAllGachaInfo);

            DialogManager.RaiseDialog(
                XamlRoot,
                "创建截图",
                content,
                true,
                "确认",
                () =>
                {
                    int gachaInfoDisplayCount = 12;

                    if (!int.TryParse(gachaInfoDisplayCountTextBox.Text, out gachaInfoDisplayCount))
                    {
                        gachaInfoDisplayCount = 12;
                    }

                    if (gachaInfoDisplayCount < 12)
                    {
                        gachaInfoDisplayCount = 12;
                    }

                    bool isShowAllGachaInfo = checkBoxShowAllGachaInfo.IsChecked == true;

                    CreateCapture_Run(
                        checkBoxShowRecords.IsChecked == true,
                        checkBoxScreenShotSelf.IsChecked == true,
                        checkBoxHideUID.IsChecked == true,
                        gachaInfoDisplayCount,
                        isShowAllGachaInfo
                    );
                }
            );
        }

        private async void CreateCapture_Run(bool isShowRecords, bool isScreenShotSelf, bool isHideUID, int gachaInfoDisplayCount, bool isShowAllGachaInfo)
        {
            WaitOverlayManager.RaiseWaitOverlay(true, "等待...", "");

            await Task.Delay(100);

            var tcs = new TaskCompletionSource<bool>();

            if (gachaInfoDisplayCount < 12)
            {
                gachaInfoDisplayCount = 12;
            }

            ScreenShotGacha.isShowGachaRecords = isShowRecords;
            ScreenShotGacha.isScreenShotSelf = isScreenShotSelf;
            ScreenShotGacha.isHideUID = isHideUID;
            ScreenShotGacha.isFinished = false;
            ScreenShotGacha.FilePath = null;
            ScreenShotGacha.GachaInfoDisplayCount = gachaInfoDisplayCount;
            ScreenShotGacha.isShowAllGachaInfo = isShowAllGachaInfo;

            var screenShotGacha = new ScreenShotGacha
            {
                TaskCompletionSource = tcs
            };

            var window = new Window
            {
                Content = screenShotGacha,
                Title = selectedUid + "_ScreenShotGachaView"
            };

            screenShotGacha.CurrentWindow = window;

            IntPtr hWnd = WindowNative.GetWindowHandle(window);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            var presenter = appWindow.Presenter as OverlappedPresenter;

            if (presenter != null)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
            }

            float scale = (float)User32.GetDpiForWindow(hWnd) / 96;

            int screenshotWidth = isShowRecords ? 900 : 550;
            int screenshotHeight = 615;

            appWindow.Resize(new SizeInt32(
                (int)(screenshotWidth * scale),
                (int)(screenshotHeight * scale)
            ));

            window.Activate();

            if (ScreenShotGacha.isScreenShotSelf)
            {
                appWindow.Hide();
            }

            window.Closed += (s, args) =>
            {
                CreateCapture.IsEnabled = true;
                WaitOverlayManager.RaiseWaitOverlay(false);

                tcs.TrySetResult(ScreenShotGacha.isFinished);

                ScreenShotGacha.isFinished = false;
                ScreenShotGacha.isShowGachaRecords = false;
                ScreenShotGacha.isScreenShotSelf = false;
                ScreenShotGacha.isHideUID = true;
                ScreenShotGacha.GachaInfoDisplayCount = 12;
                ScreenShotGacha.isShowAllGachaInfo = false;
            };

            CreateCapture.IsEnabled = false;

            bool isScreenShotFinished = await tcs.Task;

            if (isScreenShotFinished)
            {
                WaitOverlayManager.RaiseWaitOverlay(false, "", "");

                string screenshotDisplayPath = string.Empty;

                if (!string.IsNullOrWhiteSpace(ScreenShotGacha.FilePath))
                {
                    screenshotDisplayPath = Path.Combine(
                        AppDataController.GetDataPath("GachaScreenshots"),
                        Path.GetFileName(ScreenShotGacha.FilePath)
                    );
                }

                NotificationManager.RaiseNotification(
                    "截图完成",
                    "截图已保存到\n" + screenshotDisplayPath,
                    InfoBarSeverity.Success,
                    false,
                    3,
                    () => CommonHelpers.FileHelpers.OpenFileLocation(ScreenShotGacha.FilePath),
                    "打开文件夹"
                );
            }
        }
    }
}