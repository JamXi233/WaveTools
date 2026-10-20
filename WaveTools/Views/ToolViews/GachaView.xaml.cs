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
using Windows.ApplicationModel.DataTransfer;
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

        private bool isRefreshingGachaRecords = false;
        private bool isChangingGachaUidProgrammatically = false;
        private int gachaRecordsLoadToken = 0;

        private bool _hasLoadedOnce;
        private bool _isRefreshingTempGachaViewByHotkey;
        private bool _isCreatingCapture;

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

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            UpdateGamePathDependentControls();
        }

        private async void GachaView_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateGamePathDependentControls();

            if (_hasLoadedOnce)
            {
                return;
            }

            _hasLoadedOnce = true;

            cardPoolInfo = await GetCardPoolInfo();

            if (cardPoolInfo == null || cardPoolInfo.CardPools == null)
            {
                Console.WriteLine("无法获取卡池信息或卡池列表为空");
                return;
            }

            await ReloadGachaViewAsync();
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

        private bool HasValidGamePathForGacha()
        {
            string gamePath = AppDataController.GetGamePath();
            return !string.IsNullOrWhiteSpace(gamePath) &&
                   !string.Equals(gamePath, "Null", StringComparison.OrdinalIgnoreCase) &&
                   !gamePath.Contains("Null");
        }

        private void UpdateGamePathDependentControls()
        {
            bool hasGamePath = HasValidGamePathForGacha();
            GetGachaURL.IsEnabled = hasGamePath && !isRefreshingGachaRecords;
            UpdateGacha.IsEnabled = hasGamePath && !isRefreshingGachaRecords;
            CreateCapture.IsEnabled = !_isCreatingCapture && !isRefreshingGachaRecords &&
                !string.IsNullOrWhiteSpace(selectedUid) && selectedCardPoolId > 0 &&
                gachaView.Visibility == Visibility.Visible;
        }

        private async void GetGachaURL_Click(object sender, RoutedEventArgs e)
        {
            UpdateGamePathDependentControls();

            if (!HasValidGamePathForGacha())
            {
                NotificationManager.RaiseNotification("未选择游戏路径", "请先在启动游戏页面选择游戏本体路径", InfoBarSeverity.Warning, true, 2);
                return;
            }

            string failureTitle = "获取抽卡链接失败";
            try
            {
                string recordsBasePath = AppDataController.GetDataPath("GachaRecords");
                string gachaLinksJson = await ProcessRun.WaveToolsHelperAsync($"/GetGachaURL {AppDataController.GetGamePathForHelper()}");
                var gachaUrls = JsonConvert.DeserializeObject<List<GachaModel.GachaUrl>>(gachaLinksJson);
                gachaUrls = gachaUrls?.Where(url => url != null && !string.IsNullOrWhiteSpace(url.PlayerId)).ToList();
                failureTitle = "打开UID选择失败";

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
                    Text = "选择UID后可保存或复制抽卡链接\n再次保存[已存在]的UID来更新抽卡链接",
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
                dialog.IsPrimaryButtonEnabled = comboBox.IsEnabled && comboBox.SelectedItem != null;
                dialog.IsSecondaryButtonEnabled = dialog.IsPrimaryButtonEnabled;

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary && comboBox.SelectedItem != null && comboBox.IsEnabled)
                {
                    string selectedUid = comboBox.SelectedItem as string;

                    if (selectedUid.Contains("[已存在]"))
                    {
                        selectedUid = selectedUid.Replace("[已存在]", "").Trim();
                    }

                    failureTitle = "保存抽卡链接失败";
                    await SaveGachaLink(selectedUid);
                }
                else if (result == ContentDialogResult.Secondary && comboBox.SelectedItem != null && comboBox.IsEnabled)
                {
                    failureTitle = "复制抽卡链接失败";
                    string selectedUid = comboBox.SelectedItem as string;

                    if (selectedUid.Contains("[已存在]"))
                    {
                        selectedUid = selectedUid.Replace("[已存在]", "").Trim();
                    }

                    var selectedGachaUrl = gachaUrls.FirstOrDefault(url => url.PlayerId == selectedUid)?.GachaLink;

                    if (string.IsNullOrWhiteSpace(selectedGachaUrl))
                    {
                        throw new InvalidOperationException("所选UID没有可复制的抽卡链接，请在游戏中打开抽卡记录后重新获取。");
                    }

                    var package = new DataPackage();
                    package.SetText(selectedGachaUrl);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                    Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
                    NotificationManager.RaiseNotification("复制成功", "抽卡记录URL已复制完成", InfoBarSeverity.Success, true, 2);
                }
            }
            catch (Exception ex)
            {
                Logging.Write($"{failureTitle}: {ex}", 2);
                NotificationManager.RaiseNotification(failureTitle, ex.Message, InfoBarSeverity.Error, true, 5);
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
            UpdateGamePathDependentControls();

            if (!HasValidGamePathForGacha())
            {
                NotificationManager.RaiseNotification("未选择游戏路径", "请先在启动游戏页面选择游戏本体路径", InfoBarSeverity.Warning, true, 2);
                return;
            }

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
                WaitOverlayManager.RaiseWaitOverlay(true, "正在更新抽卡记录", "请稍等片刻", true, 0);

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

                if (!updateSuccess)
                {
                    gacha_status.Text = "抽卡记录更新失败";
                    await ReloadGachaViewAsync(uidToUpdate, preferredCardPoolId);
                    return;
                }

                gacha_status.Text = "抽卡记录更新完成，正在刷新...";

                selectedCardPoolId = 0;
                await ReloadGachaViewAsync(uidToUpdate, null, true);

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
                WaitOverlayManager.RaiseWaitOverlay(false);
                isRefreshingGachaRecords = false;

                UpdateGamePathDependentControls();
                ClearGacha.IsEnabled = !string.IsNullOrWhiteSpace(selectedUid);
                GachaRecordsUID.IsEnabled = true;
                GachaPoolComboBox.IsEnabled = GachaPoolComboBox.Items.Count > 0;
            }
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

        private async Task SaveGachaLink(string UID)
        {
            WaitOverlayManager.RaiseWaitOverlay(true, "正在保存抽卡链接", "请稍等片刻", true, 0);

            try
            {
                await ProcessRun.WaveToolsHelperAsync($"/SaveGachaURL {AppDataController.GetGamePathForHelper()} {UID}");
                WaitOverlayManager.RaiseWaitOverlay(true, "正在获取抽卡记录", "请稍等片刻", true, 0);
                await GetGachaRecords(UID, false);
            }
            finally
            {
                WaitOverlayManager.RaiseWaitOverlay(false);
            }
        }

        private async Task<bool> GetGachaRecords(string UID, bool manageWaitOverlay = true)
        {
            if (manageWaitOverlay)
            {
                WaitOverlayManager.RaiseWaitOverlay(true, "正在获取抽卡记录", "请稍等片刻", true, 0);
            }

            try
            {
                var result = await ProcessRun.WaveToolsHelperAsync($"/GetGachaRecords {UID}");

                Logging.Write(result);

                bool isSuccess = HandleGachaRecordsResult(result, "获取完成", UID);

                if (isSuccess)
                {
                    selectedUid = UID;
                    selectedCardPoolId = 0;
                    await ReloadGachaViewAsync(UID, null, true);
                }

                return isSuccess;
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

                return false;
            }
            finally
            {
                if (manageWaitOverlay)
                {
                    WaitOverlayManager.RaiseWaitOverlay(false);
                }
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
            await ReloadGachaViewAsync(selectedUid, selectedCardPoolId > 0 ? selectedCardPoolId : null);
        }

        private async Task ReloadGachaViewAsync(string preferredUid = null, int? preferredCardPoolId = null, bool forceFirstCardPool = false)
        {
            UpdateGamePathDependentControls();

            string previousUid = selectedUid;

            await LoadUIDs(preferredUid);

            string uid = GetSelectedUidFromComboBox();

            if (string.IsNullOrWhiteSpace(uid))
            {
                selectedUid = null;
                selectedCardPoolId = 0;
                ClearGacha.IsEnabled = false;
                OpenGachaWeb.IsEnabled = false;
                ExportWWGF.IsEnabled = false;
                GachaPoolComboBox.IsEnabled = false;
                gachaView.Visibility = Visibility.Collapsed;
                loadGachaProgress.Visibility = Visibility.Collapsed;
                noGachaFound.Visibility = Visibility.Visible;
                UpdateGamePathDependentControls();
                return;
            }

            bool uidChanged = !string.Equals(previousUid, uid, StringComparison.Ordinal);
            bool shouldForceFirstCardPool = forceFirstCardPool || uidChanged;

            selectedUid = uid;

            if (shouldForceFirstCardPool)
            {
                selectedCardPoolId = 0;
            }

            ClearGacha.IsEnabled = true;
            OpenGachaWeb.IsEnabled = true;
            ExportWWGF.IsEnabled = true;

            await LoadGachaRecords(uid, preferredCardPoolId, shouldForceFirstCardPool);

        }

        private void ComboBox_Click(object sender, object e)
        {
        }

        private Task LoadUIDs(string preferredUid = null)
        {
            bool oldProgrammaticState = isChangingGachaUidProgrammatically;
            isChangingGachaUidProgrammatically = true;

            GachaRecordsUID.SelectionChanged -= GachaRecordsUID_SelectionChanged;
            GachaRecordsUID.ItemsSource = null;
            GachaRecordsUID.Items.Clear();

            GachaPoolComboBox.SelectionChanged -= GachaPoolComboBox_SelectionChanged;
            GachaPoolComboBox.Items.Clear();
            GachaPoolComboBox.SelectedItem = null;
            GachaPoolComboBox.IsEnabled = false;
            GachaPoolComboBox.Width = 120;
            GachaPoolComboBox.SelectionChanged += GachaPoolComboBox_SelectionChanged;

            try
            {
                string linkDirectory = AppDataController.GetDataPath("GachaLinks");
                string recordsDirectory = AppDataController.GetDataPath("GachaRecords");

                SortedSet<string> recordUidSet = new SortedSet<string>(StringComparer.Ordinal);
                SortedSet<string> linkUidSet = new SortedSet<string>(StringComparer.Ordinal);

                if (Directory.Exists(recordsDirectory))
                {
                    var recordFiles = Directory.GetFiles(recordsDirectory, "*.json");

                    foreach (var file in recordFiles)
                    {
                        string uid = Path.GetFileNameWithoutExtension(file);

                        if (!string.IsNullOrWhiteSpace(uid))
                        {
                            recordUidSet.Add(uid);
                        }
                    }
                }

                if (Directory.Exists(linkDirectory))
                {
                    var linkFiles = Directory.GetFiles(linkDirectory, "*.json");

                    foreach (var file in linkFiles)
                    {
                        string uid = Path.GetFileNameWithoutExtension(file);

                        if (!string.IsNullOrWhiteSpace(uid))
                        {
                            linkUidSet.Add(uid);
                        }
                    }
                }

                List<string> uidList = recordUidSet
                    .Concat(linkUidSet.Where(uid => !recordUidSet.Contains(uid)))
                    .ToList();

                if (uidList.Count == 0)
                {
                    selectedUid = null;
                    loadGachaProgress.Visibility = Visibility.Collapsed;
                    noGachaFound.Visibility = Visibility.Visible;
                    gachaView.Visibility = Visibility.Collapsed;
                    ClearGacha.IsEnabled = false;
                    OpenGachaWeb.IsEnabled = false;
                    ExportWWGF.IsEnabled = false;
                    return Task.CompletedTask;
                }

                GachaRecordsUID.ItemsSource = uidList;

                string uidToSelect = !string.IsNullOrWhiteSpace(preferredUid) && uidList.Contains(preferredUid)
                    ? preferredUid
                    : uidList[0];

                GachaRecordsUID.SelectedItem = uidToSelect;
                selectedUid = uidToSelect;

                loadGachaProgress.Visibility = Visibility.Collapsed;
                noGachaFound.Visibility = Visibility.Collapsed;
                gachaView.Visibility = Visibility.Visible;
                ClearGacha.IsEnabled = true;
                OpenGachaWeb.IsEnabled = true;
                ExportWWGF.IsEnabled = true;
            }
            catch (Exception ex)
            {
                NotificationManager.RaiseNotification("抽卡分析", "加载UID时出现错误", InfoBarSeverity.Error);
                Logging.Write($"加载UID时出现错误: {ex.Message}", 2);
            }
            finally
            {
                GachaRecordsUID.SelectionChanged += GachaRecordsUID_SelectionChanged;
                isChangingGachaUidProgrammatically = oldProgrammaticState;
            }

            return Task.CompletedTask;
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
                UpdateGamePathDependentControls();

                if (gachaFrame != null)
                {
                    gachaFrame.Visibility = Visibility.Collapsed;
                }
            }

            Logging.Write($"GachaUID:{selectedUid}");
            await LoadGachaRecords(selectedUid, null, isDifferentUid);
        }

        private async Task LoadGachaRecords(string uid = null, int? preferredCardPoolId = null, bool forceFirstCardPool = false)
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
            UpdateGamePathDependentControls();

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

            DisplayGachaData(gachaData, preferredCardPoolId, forceFirstCardPool);
        }

        private void DisplayGachaData(GachaModel.GachaData gachaData, int? preferredCardPoolId = null, bool forceFirstCardPool = false)
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

                if (!forceFirstCardPool && preferredCardPoolId.HasValue)
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

                if (!forceFirstCardPool && targetItem == null && selectedCardPoolId > 0)
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
                UpdateGamePathDependentControls();
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

            string preferredUid = selectedUid;
            int preferredCardPoolId = selectedCardPoolId;

            _isRefreshingTempGachaViewByHotkey = true;

            try
            {
                Logging.Write("Refresh GachaView by F5", 0);
                await ReloadGachaViewAsync(preferredUid, preferredCardPoolId);
            }
            catch (Exception ex)
            {
                Logging.Write($"Refresh GachaView failed: {ex.Message}", 2);
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
            UpdateGamePathDependentControls();
        }

        private async void ImportExportWWGF_Click(object sender, RoutedEventArgs e)
        {
            var contentPanel = new StackPanel
            {
                Spacing = 10,
                Width = 360
            };

            contentPanel.Children.Add(new TextBlock
            {
                Text = "导入功能为实验性功能",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.WrapWholeWords
            });

            contentPanel.Children.Add(new TextBlock
            {
                Text = "由于目前没有固定统一的抽卡记录格式，不同工具导出的字段、卡池、时间等标识可能存在差异，导入时可能失败、缺少部分记录或无法识别部分卡池。建议导入前先备份当前抽卡记录。",
                TextWrapping = TextWrapping.WrapWholeWords,
                Opacity = 0.72
            });

            contentPanel.Children.Add(new TextBlock
            {
                Text = "导出会把当前选中 UID 的抽卡记录导出为 Wuthering Waves Gacha Format JSON。",
                TextWrapping = TextWrapping.WrapWholeWords,
                Opacity = 0.72
            });

            var dialog = new ContentDialog
            {
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                Title = "导入/导出抽卡记录",
                Content = contentPanel,
                PrimaryButtonText = "导入",
                SecondaryButtonText = "导出",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            bool canExport = !string.IsNullOrWhiteSpace(selectedUid) && File.Exists(Path.Combine(AppDataController.GetDataPath("GachaRecords"), $"{selectedUid}.json"));
            dialog.IsSecondaryButtonEnabled = canExport;

            ContentDialogResult result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await ImportWWGFAsync();
            }
            else if (result == ContentDialogResult.Secondary)
            {
                await ExportWWGFAsync();
            }
        }

        private async void ExportWWGF_Click(object sender, RoutedEventArgs e)
        {
            await ExportWWGFAsync();
        }

        private async Task ExportWWGFAsync()
        {
            if (string.IsNullOrWhiteSpace(selectedUid))
            {
                NotificationManager.RaiseNotification("导出失败", "请先选择需要导出的UID", InfoBarSeverity.Warning, true, 2);
                return;
            }

            string recordsBasePath = AppDataController.GetDataPath("GachaRecords");
            string sourceFilePath = Path.Combine(recordsBasePath, $"{selectedUid}.json");

            if (!File.Exists(sourceFilePath))
            {
                NotificationManager.RaiseNotification("导出失败", "当前UID没有可导出的抽卡记录", InfoBarSeverity.Warning, true, 2);
                return;
            }

            DateTime now = DateTime.Now;
            string formattedDate = now.ToString("yyyy_MM_dd_HH_mm_ss");

            var suggestFileName = $"WaveTools_Gacha_Export_{selectedUid}_{formattedDate}";

            var fileTypeChoices = new Dictionary<string, List<string>>
            {
                { "Wuthering Waves Gacha Format", new List<string> { ".json" } }
            };

            var defaultExtension = ".json";

            string filePath = await CommonHelpers.FileHelpers.SaveFile(suggestFileName, fileTypeChoices, defaultExtension);

            if (filePath == null)
            {
                return;
            }

            try
            {
                ExportGacha.Export(sourceFilePath, filePath);
                NotificationManager.RaiseNotification("导出成功", "抽卡记录已导出到\n" + filePath, InfoBarSeverity.Success, true, 2);
            }
            catch (Exception ex)
            {
                Logging.Write($"Export WWGF failed: {ex.Message}", 2);
                NotificationManager.RaiseNotification("导出失败", ex.Message, InfoBarSeverity.Error, true, 5);
            }
        }

        private async void ImportWWGF_Click(object sender, RoutedEventArgs e)
        {
            await ImportWWGFAsync();
        }

        private async Task ImportWWGFAsync()
        {
            string filePath = await CommonHelpers.FileHelpers.OpenFile(".json");

            if (filePath == null)
            {
                return;
            }

            try
            {
                string importJson = await File.ReadAllTextAsync(filePath);
                var importData = JsonConvert.DeserializeObject<ImportGacha.ImportData>(importJson);
                string importedUid = importData?.info?.uid;

                await ImportGacha.Import(filePath);

                if (!string.IsNullOrWhiteSpace(importedUid))
                {
                    selectedUid = importedUid;
                }

                await ReloadGachaViewAsync(importedUid, null, true);
            }
            catch (Exception ex)
            {
                Logging.Write($"Import WWGF failed: {ex.Message}", 2);
                NotificationManager.RaiseNotification("导入失败", ex.Message, InfoBarSeverity.Error, true, 5);
            }
        }

        private void ClearGacha_Click(object sender, RoutedEventArgs e)
        {
            ClearGachaText.Text = $"将清空的UID:{selectedUid}";
            ClearGachaTip.IsOpen = true;
        }

        private async void ConfirmClearGachaRecords(object sender, RoutedEventArgs e)
        {
            ClearGachaTip.IsOpen = false;
            await ClearGacha_Run(0);
        }

        private async void ConfirmClearGachaRecordsAndUID(object sender, RoutedEventArgs e)
        {
            ClearGachaTip.IsOpen = false;
            await ClearGacha_Run(1);
        }

        private async Task<bool> ConfirmDeleteGachaRecordsAsync(int clearGachaMode)
        {
            string title = clearGachaMode == 0 ? "确认删除抽卡记录？" : "确认删除抽卡记录和抽卡链接？";
            string content = clearGachaMode == 0
                ? $"将删除 UID:{selectedUid} 的抽卡记录，此操作不可撤销。"
                : $"将删除 UID:{selectedUid} 的抽卡记录和抽卡链接，此操作不可撤销。";

            var dialog = new ContentDialog
            {
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                Title = title,
                Content = content,
                PrimaryButtonText = "删除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            ContentDialogResult result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        private async Task ClearGacha_Run(int clearGachaMode)
        {
            if (string.IsNullOrWhiteSpace(selectedUid))
            {
                return;
            }

            if (!await ConfirmDeleteGachaRecordsAsync(clearGachaMode))
            {
                return;
            }

            if (clearGachaMode == 0)
            {
                await ProcessRun.WaveToolsHelperAsync($"/DeleteGachaRecords {selectedUid}");
            }
            else if (clearGachaMode == 1)
            {
                await ProcessRun.WaveToolsHelperAsync($"/DeleteGachaRecords {selectedUid}");
                await ProcessRun.WaveToolsHelperAsync($"/DeleteGachaUID {selectedUid}");
            }

            selectedUid = null;
            selectedCardPoolId = 0;
            await ReloadGachaViewAsync(null, null, true);
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
            if (_isCreatingCapture || isRefreshingGachaRecords || string.IsNullOrWhiteSpace(selectedUid) ||
                selectedCardPoolId <= 0 || gachaView.Visibility != Visibility.Visible)
            {
                return;
            }

            _isCreatingCapture = true;
            UpdateGamePathDependentControls();
            try
            {
                // 选项和记录始终对应打开对话框时选中的 UID、卡池。
                var options = new GachaReportOptions
                {
                    Uid = selectedUid,
                    PoolId = selectedCardPoolId,
                    Rule = JsonConvert.DeserializeObject<GachaModel.CardPool>(JsonConvert.SerializeObject(
                        cardPoolInfo?.CardPools?.FirstOrDefault(pool => pool.CardPoolId == selectedCardPoolId)))
                };
                var autoSave = new CheckBox { Content = "生成后直接保存", IsChecked = true };
                var hideUid = new CheckBox { Content = "隐藏 UID", IsChecked = true };
                var showRecords = new CheckBox { Content = "附带最近抽卡记录", IsChecked = false };
                var showStatistics = new CheckBox { Content = "附带统计图表", IsChecked = false };
                var showAll = new CheckBox { Content = "全部五星明细", IsChecked = false };
                var count = new NumberBox
                {
                    Value = 12, Minimum = 1, Maximum = 10000, Width = 140,
                    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
                };
                showAll.Checked += (_, _) => count.IsEnabled = false;
                showAll.Unchecked += (_, _) => count.IsEnabled = true;
                var countRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
                countRow.Children.Add(new TextBlock { Text = "五星明细条数", VerticalAlignment = VerticalAlignment.Center });
                countRow.Children.Add(count);
                var content = new StackPanel { Spacing = 10, MinWidth = 300 };
                content.Children.Add(hideUid);
                content.Children.Add(autoSave);
                content.Children.Add(countRow);
                content.Children.Add(showAll);
                content.Children.Add(showStatistics);
                content.Children.Add(showRecords);
                content.Children.Add(new TextBlock { Text = "高清 PNG · 纵向长图", FontSize = 12, Opacity = 0.65 });
                var dialog = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = "抽卡截图",
                    Content = content,
                    PrimaryButtonText = "生成",
                    CloseButtonText = "取消",
                    DefaultButton = ContentDialogButton.Primary
                };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
                options.AutoSave = autoSave.IsChecked == true;
                options.HideUid = hideUid.IsChecked == true;
                options.ShowRecords = showRecords.IsChecked == true;
                options.ShowStatistics = showStatistics.IsChecked == true;
                options.ShowAllDetails = showAll.IsChecked == true;
                options.DetailCount = double.IsNaN(count.Value) ? 12 : (int)Math.Clamp(count.Value, 1, 10000);
                await CreateCaptureAsync(options);
            }
            catch (Exception ex)
            {
                Logging.Write($"CreateCapture failed: {ex}", 2);
                NotificationManager.RaiseNotification("创建截图失败", ex.Message, InfoBarSeverity.Error, false, 6);
            }
            finally
            {
                _isCreatingCapture = false;
                UpdateGamePathDependentControls();
            }
        }

        private async Task CreateCaptureAsync(GachaReportOptions options)
        {
            Window window = null;
            ScreenShotGacha report = null;
            bool closed = false;
            bool ownsOverlay = true;
            void HideOverlay()
            {
                if (!ownsOverlay) return;
                ownsOverlay = false;
                WaitOverlayManager.RaiseWaitOverlay(false);
            }
            WaitOverlayManager.RaiseWaitOverlay(true, "正在生成抽卡截图", "请稍等", true, 0);
            try
            {
                report = new ScreenShotGacha(options);
                window = new Window { Content = report, Title = "抽卡截图预览" };
                report.CurrentWindow = window;
                report.ReportReady += (_, _) => HideOverlay();
                window.Closed += (_, _) =>
                {
                    closed = true;
                    report.NotifyWindowClosed();
                    HideOverlay();
                };
                IntPtr handle = WindowNative.GetWindowHandle(window);
                WindowId id = Win32Interop.GetWindowIdFromWindow(handle);
                AppWindow appWindow = AppWindow.GetFromWindowId(id);
                float scale = User32.GetDpiForWindow(handle) / 96f;
                var workArea = DisplayArea.GetFromWindowId(id, DisplayAreaFallback.Nearest).WorkArea;
                var size = new SizeInt32(Math.Min((int)(1180 * scale), (int)(workArea.Width * 0.92)),
                    Math.Min((int)(900 * scale), (int)(workArea.Height * 0.9)));
                appWindow.Resize(size);
                appWindow.Move(new PointInt32(workArea.X + (workArea.Width - size.Width) / 2,
                    workArea.Y + (workArea.Height - size.Height) / 2));
                window.Activate();
                if (await report.Completion)
                {
                    string firstFile = report.SavedFiles.FirstOrDefault();
                    NotificationManager.RaiseNotification("截图已保存",
                        $"共 {report.SavedFiles.Count} 张 PNG\n{Path.GetDirectoryName(firstFile)}",
                        InfoBarSeverity.Success, false, 5,
                        () => CommonHelpers.FileHelpers.OpenFileLocation(firstFile), "打开文件夹");
                }
            }
            finally
            {
                if (window != null && !closed) window.Close();
                report?.NotifyWindowClosed();
                HideOverlay();
            }
        }
    }
}
