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
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json;
using WaveTools.Depend;
using WaveTools.Views.ToolViews;
using Windows.UI;
using static WaveTools.App;

namespace WaveTools.Views.GachaViews
{
    public sealed partial class TempGachaView : Page
    {
        private const string WuwaAssetsApiUrl = "https://cdn.jamsg.cn/?get=wuwa-assets";
        private static readonly HttpClient _httpClient = new HttpClient();
        private static WuwaAssetsCacheModel _wuwaAssetsCache;
        private static DateTime _wuwaAssetsCacheLoadedAt = DateTime.MinValue;

        private bool _isLoadingData;
        private int _loadDataToken;

        public TempGachaView()
        {
            this.InitializeComponent();
            Logging.Write("Switch to TempGachaView", 0);
            _ = RefreshDataAsync();
        }

        public async Task RefreshDataAsync()
        {
            if (_isLoadingData)
            {
                return;
            }

            int currentLoadToken = ++_loadDataToken;
            _isLoadingData = true;

            try
            {
                ClearDisplayedData();
                await LoadDataAsync(currentLoadToken);
            }
            finally
            {
                if (currentLoadToken == _loadDataToken)
                {
                    _isLoadingData = false;
                }
            }
        }

        private async Task LoadDataAsync(int currentLoadToken)
        {
            try
            {
                Logging.Write("Starting LoadData method", 0);
                string selectedUID = GachaView.selectedUid;
                int selectedCardPoolId = GachaView.selectedCardPoolId;
                Logging.Write($"Selected UID: {selectedUID}, Selected Card Pool ID: {selectedCardPoolId}", 0);

                if (string.IsNullOrWhiteSpace(selectedUID) || selectedCardPoolId <= 0)
                {
                    Logging.Write("LoadData skipped because selected UID or card pool ID is empty", 1);
                    return;
                }

                string recordsDirectory = AppDataController.GetDataPath("GachaRecords");
                string filePath = Path.Combine(recordsDirectory, $"{selectedUID}.json");
                Logging.Write($"Records Directory: {recordsDirectory}, File Path: {filePath}", 0);

                if (!File.Exists(filePath))
                {
                    Logging.Write("File not found: " + filePath, 1);
                    Console.WriteLine("找不到UID的抽卡记录文件");
                    return;
                }

                Logging.Write("Reading file content", 0);
                string jsonContent = await File.ReadAllTextAsync(filePath);

                if (currentLoadToken != _loadDataToken)
                {
                    return;
                }

                Logging.Write("Deserializing JSON content", 0);
                var gachaData = JsonConvert.DeserializeObject<GachaModel.GachaData>(jsonContent);

                if (gachaData?.List == null)
                {
                    Logging.Write("Gacha data is empty or invalid", 1);
                    return;
                }

                var records = gachaData.List
                    .Where(pool => pool.CardPoolId == selectedCardPoolId && pool.Records != null)
                    .SelectMany(pool => pool.Records)
                    .ToList();

                Logging.Write($"Total records found: {records.Count}", 0);

                if (records.Count == 0)
                {
                    Logging.Write("Current card pool has no records", 1);
                    return;
                }

                bool hasAnyRecordWithId = records.Any(r => !string.IsNullOrWhiteSpace(r.Id));

                if (!hasAnyRecordWithId)
                {
                    Logging.Write("Current records are legacy format without id field. Generate temporary display id.", 1);

                    GenerateTemporaryDisplayIds(records, selectedCardPoolId);

                    NotificationManager.RaiseNotification(
                        $"UID:{selectedUID}的抽卡记录是旧版本格式",
                        "已临时兼容显示旧版抽卡记录。\n建议之后更新抽卡记录以升级为新版格式。",
                        InfoBarSeverity.Warning,
                        false,
                        5
                    );
                }
                else
                {
                    GenerateMissingTemporaryDisplayIds(records, selectedCardPoolId);
                }

                if (currentLoadToken != _loadDataToken)
                {
                    return;
                }

                // 筛选出四星和五星的记录
                var rank4Records = records.Where(r => r.QualityLevel == 4).ToList();
                var rank5Records = records.Where(r => r.QualityLevel == 5).ToList();
                Logging.Write($"4-star records count: {rank4Records.Count}, 5-star records count: {rank5Records.Count}", 0);

                // 显示记录详情
                DisplayGachaDetails(gachaData, rank4Records, rank5Records, selectedCardPoolId, GachaView.cardPoolInfo);

                // 显示抽卡详情
                DisplayGachaInfo(records, selectedCardPoolId);

                // 显示抽卡记录
                DisplayGachaRecords(records);
                Logging.Write("LoadData method finished", 0);
            }
            catch (Exception ex)
            {
                Logging.Write($"LoadData failed: {ex.Message}", 2);
                NotificationManager.RaiseNotification("抽卡详情刷新失败", ex.Message, InfoBarSeverity.Error, false, 5);
            }
        }

        private void ClearDisplayedData()
        {
            Gacha_Panel.Children.Clear();
            GachaInfo_List.ItemsSource = null;
            GachaRecords_List.ItemsSource = null;
            GachaInfo_List_Disable.Visibility = Visibility.Collapsed;
            Gacha_UID.Text = "UID";
            GachaInfo_SinceLast5Star.Text = "SinceLast5Star";
            GachaRecords_Count.Text = "Count";
        }

        private void GenerateTemporaryDisplayIds(List<GachaModel.GachaRecord> records, int cardPoolId)
        {
            if (records == null || records.Count == 0)
            {
                return;
            }

            var timestampCounter = new Dictionary<long, int>();

            foreach (var record in records.OrderByDescending(r => r.Time).ToList())
            {
                if (record == null || string.IsNullOrWhiteSpace(record.Time))
                {
                    continue;
                }

                long timestamp;

                try
                {
                    timestamp = DateTimeOffset.Parse(record.Time).ToUnixTimeSeconds();
                }
                catch
                {
                    timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                }

                if (!timestampCounter.ContainsKey(timestamp))
                {
                    int sameTimestampCount = records.Count(r =>
                    {
                        if (r == null || string.IsNullOrWhiteSpace(r.Time))
                        {
                            return false;
                        }

                        try
                        {
                            return DateTimeOffset.Parse(r.Time).ToUnixTimeSeconds() == timestamp;
                        }
                        catch
                        {
                            return false;
                        }
                    });

                    timestampCounter[timestamp] = Math.Min(sameTimestampCount, 10);
                }

                int drawNumber = timestampCounter[timestamp];
                timestampCounter[timestamp]--;

                record.Id = $"{timestamp}{cardPoolId:D4}{drawNumber:D4}";
            }
        }

        private void GenerateMissingTemporaryDisplayIds(List<GachaModel.GachaRecord> records, int cardPoolId)
        {
            if (records == null || records.Count == 0)
            {
                return;
            }

            var recordsWithoutId = records
                .Where(r => r != null && string.IsNullOrWhiteSpace(r.Id))
                .ToList();

            if (recordsWithoutId.Count == 0)
            {
                return;
            }

            var timestampCounter = new Dictionary<long, int>();

            foreach (var record in recordsWithoutId.OrderByDescending(r => r.Time).ToList())
            {
                if (string.IsNullOrWhiteSpace(record.Time))
                {
                    continue;
                }

                long timestamp;

                try
                {
                    timestamp = DateTimeOffset.Parse(record.Time).ToUnixTimeSeconds();
                }
                catch
                {
                    timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                }

                if (!timestampCounter.ContainsKey(timestamp))
                {
                    int sameTimestampCount = recordsWithoutId.Count(r =>
                    {
                        if (r == null || string.IsNullOrWhiteSpace(r.Time))
                        {
                            return false;
                        }

                        try
                        {
                            return DateTimeOffset.Parse(r.Time).ToUnixTimeSeconds() == timestamp;
                        }
                        catch
                        {
                            return false;
                        }
                    });

                    timestampCounter[timestamp] = Math.Min(sameTimestampCount, 10);
                }

                int drawNumber = timestampCounter[timestamp];
                timestampCounter[timestamp]--;

                record.Id = $"{timestamp}{cardPoolId:D4}{drawNumber:D4}";
            }
        }

        private void DisplayGachaRecords(List<GachaModel.GachaRecord> records)
        {
            Logging.Write("Displaying gacha records", 0);
            GachaRecords_List.ItemsSource = records;
        }

        private void DisplayGachaInfo(List<GachaModel.GachaRecord> records, int selectedCardPoolId)
        {
            Logging.Write("Displaying gacha info", 0);
            var selectedCardPool = GachaView.cardPoolInfo.CardPools.FirstOrDefault(cp => cp.CardPoolId == selectedCardPoolId);

            var rank5SourceRecords = records.Where(r => r.QualityLevel == 5).ToList();

            if (rank5SourceRecords.Count == 0)
            {
                GachaInfo_List_Disable.Visibility = Visibility.Visible;
                GachaInfo_List.ItemsSource = null;
                return;
            }

            GachaInfo_List_Disable.Visibility = Visibility.Collapsed;

            var rank5Records = rank5SourceRecords
                .Select(record => new GachaInfoDisplayItem
                {
                    ResourceId = record.ResourceId,
                    ResourceType = record.ResourceType,
                    Name = record.Name,
                    Count = CalculateCount(records, record.Id, 5),
                    Pity = CalculatePity(records, record.Name, 5, selectedCardPoolId, GachaView.cardPoolInfo),
                    PityVisibility = selectedCardPool != null && selectedCardPool.isPityEnable == true
                        ? Visibility.Collapsed
                        : Visibility.Collapsed
                })
                .ToList();

            GachaInfo_List.ItemsSource = rank5Records;

            _ = LoadGachaInfoIconsAsync(rank5Records, _loadDataToken);

            Logging.Write("Finished displaying gacha info base content", 0);
        }

        private async Task LoadGachaInfoIconsAsync(List<GachaInfoDisplayItem> items, int loadToken)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            try
            {
                await EnsureWuwaAssetsCacheAsync();

                if (loadToken != _loadDataToken)
                {
                    return;
                }

                foreach (var item in items)
                {
                    if (loadToken != _loadDataToken)
                    {
                        return;
                    }

                    try
                    {
                        BitmapImage iconSource = await GetGachaItemIconAsync(item.ResourceId, item.Name, item.ResourceType);

                        if (loadToken != _loadDataToken)
                        {
                            return;
                        }

                        item.IconSource = iconSource;
                    }
                    catch (Exception ex)
                    {
                        Logging.Write($"Load icon failed for {item?.Name}: {ex.Message}", 1);
                        if (item != null)
                        {
                            item.IconSource = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Write($"Load gacha info icons failed: {ex.Message}", 1);

                foreach (var item in items)
                {
                    item.IconSource = null;
                }
            }
        }

        private static readonly HashSet<string> PermanentFiveStarNames = new HashSet<string>
        {
            "维里奈", "安可", "卡卡罗", "凌阳", "鉴心"
        };

        private static bool IsPermanentFiveStar(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && PermanentFiveStarNames.Contains(name);
        }

        private string CalculateCount(List<GachaModel.GachaRecord> records, string id, int qualityLevel)
        {
            int countSinceLastTargetStar = 1;
            bool foundTargetStar = false;
            for (int i = records.Count - 1; i >= 0; i--)
            {
                var record = records[i];
                if (record.QualityLevel == qualityLevel && record.Id == id)
                {
                    foundTargetStar = true;
                    break;
                }
                if (record.QualityLevel == 5)
                {
                    countSinceLastTargetStar = 1;
                }
                else
                {
                    countSinceLastTargetStar++;
                }
            }
            if (!foundTargetStar)
            {
                return "未找到";
            }

            Logging.Write($"Count since last target star: {countSinceLastTargetStar}", 0);
            return $"{countSinceLastTargetStar}";
        }


        private string CalculatePity(List<GachaModel.GachaRecord> records, string name, int qualityLevel, int selectedCardPoolId, GachaModel.CardPoolInfo cardPoolInfo)
        {
            var selectedCardPool = cardPoolInfo?.CardPools?.FirstOrDefault(cp => cp.CardPoolId == selectedCardPoolId);

            if (selectedCardPool?.isPityEnable != true || !IsPermanentFiveStar(name))
            {
                return "";
            }

            // Logging.Write("Pity result: 歪了", 0);
            return "歪了";
        }

        private List<int> CalculateIntervals(List<GachaModel.GachaRecord> records, int qualityLevel)
        {
            var intervals = new List<int>();
            int countSinceLastStar = 0;

            // 倒序遍历记录
            foreach (var record in records.AsEnumerable().Reverse())
            {
                countSinceLastStar++; // 每次迭代都递增计数器

                if (record.QualityLevel == qualityLevel)
                {
                    intervals.Add(countSinceLastStar); // 将计数器的值添加到间隔列表中
                    countSinceLastStar = 0; // 重置计数器
                }
            }

            return intervals;
        }

        private List<int> CalculateUpIntervals(List<GachaModel.GachaRecord> records)
        {
            var intervals = new List<int>();
            int countSinceLastUpFiveStar = 0;

            // 倒序遍历记录，统计相邻两次Up五星之间的抽数（含本次Up、不含上一次Up）；
            // 第一个Up五星没有上一次Up，按从记录起点到该Up的抽数计
            foreach (var record in records.AsEnumerable().Reverse())
            {
                countSinceLastUpFiveStar++;

                if (record.QualityLevel == 5 && !IsPermanentFiveStar(record.Name))
                {
                    intervals.Add(countSinceLastUpFiveStar);
                    countSinceLastUpFiveStar = 0;
                }
            }

            return intervals;
        }

        private void DisplayGachaDetails(GachaModel.GachaData gachaData, List<GachaModel.GachaRecord> rank4Records, List<GachaModel.GachaRecord> rank5Records, int selectedCardPoolId, GachaModel.CardPoolInfo cardPoolInfo)
        {
            Logging.Write("Displaying gacha details", 0);
            Gacha_Panel.Children.Clear();
            var scrollView = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            };

            var contentPanel = new StackPanel();

            var selectedRecords = gachaData.List
                .Where(pool => pool.CardPoolId == selectedCardPoolId)
                .SelectMany(pool => pool.Records)
                .OrderByDescending(r => r.Time)
                .ToList();

            Logging.Write($"Total selected records: {selectedRecords.Count}", 0);

            int countSinceLast5Star = 0;
            int countSinceLast4Star = 0;
            bool foundLast5Star = false;
            bool foundLast4Star = false;

            foreach (var record in selectedRecords)
            {
                if (!foundLast5Star && record.QualityLevel == 5)
                {
                    foundLast5Star = true;
                    foundLast4Star = true;
                }
                else if (!foundLast5Star)
                {
                    countSinceLast5Star++;
                }

                if (!foundLast4Star && record.QualityLevel == 4)
                {
                    foundLast4Star = true;
                }
                else if (!foundLast4Star)
                {
                    countSinceLast4Star++;
                }

                if (foundLast5Star && foundLast4Star)
                {
                    break;
                }
            }


            // 计算四星和五星的间隔
            var fourStarIntervals = CalculateIntervals(selectedRecords, 4);
            var fiveStarIntervals = CalculateIntervals(selectedRecords, 5);

            // 计算平均值
            string averageDraws4Star = fourStarIntervals.Count > 0 ? (fourStarIntervals.Average()).ToString("F2") : "∞";
            string averageDraws5Star = fiveStarIntervals.Count > 0 ? (fiveStarIntervals.Average()).ToString("F2") : "∞";

            // 计算Up五星统计（非常驻五星名单的五星记录视为Up）
            var upFiveStarRecords = selectedRecords
                .Where(r => r.QualityLevel == 5 && !IsPermanentFiveStar(r.Name))
                .ToList();

            // 5星Up平均抽数：相邻两次Up五星之间的抽数；第一个Up以记录起点到该Up计
            var upFiveStarIntervals = CalculateUpIntervals(selectedRecords);

            string averageDraws5StarUp = upFiveStarRecords.Count > 0
                ? upFiveStarIntervals.Average().ToString("F2")
                : "∞";

            // 常驻五星记录（与Up记录共同构成全部五星记录）
            var permanentFiveStarRecords = selectedRecords
                .Where(r => r.QualityLevel == 5 && IsPermanentFiveStar(r.Name))
                .ToList();

            // 五星Up歪率：歪的次数 / 已产生结果的Up尝试次数。
            // 若最新的一个五星是常驻（歪了），该次尝试的结果已经发生但还没有对应的Up结算，
            // 分母需要加 1；否则分母就是Up五星数。
            var latestFiveStar = selectedRecords.FirstOrDefault(r => r.QualityLevel == 5);
            bool isLatestFiveStarPermanent = latestFiveStar != null && IsPermanentFiveStar(latestFiveStar.Name);
            int upAttemptCount = upFiveStarRecords.Count + (isLatestFiveStarPermanent ? 1 : 0);

            string upLossRate = upAttemptCount > 0
                ? (permanentFiveStarRecords.Count / (double)upAttemptCount * 100).ToString("F2") + "%"
                : "∞";

            Gacha_UID.Text = gachaData.Info.Uid;
            GachaRecords_Count.Text = "共" + selectedRecords.Count() + "抽";
            GachaInfo_SinceLast5Star.Text = $"垫了{countSinceLast5Star}发";

            var selectedCardPool = cardPoolInfo.CardPools.FirstOrDefault(cp => cp.CardPoolId == selectedCardPoolId);

            string rate4Star = rank4Records.Count > 0
                ? (rank4Records.Count / (double)selectedRecords.Count * 100).ToString("F2") + "%"
                : "∞";

            string rate5Star = rank5Records.Count > 0
                ? (rank5Records.Count / (double)selectedRecords.Count * 100).ToString("F2") + "%"
                : "∞";

            string latest5StarTime = rank5Records.Any() ? rank5Records.First().Time : "∞";
            string latest4StarTime = rank4Records.Any() ? rank4Records.First().Time : "∞";

            string poolName = selectedCardPool?.CardPoolType ?? $"卡池 {selectedCardPoolId}";

            contentPanel.Children.Add(CreateOverviewCard(
                poolName,
                selectedRecords.Count,
                selectedRecords.Count * 160,
                latest5StarTime,
                latest4StarTime
            ));

            if (selectedCardPool != null && selectedCardPool.FiveStarPity.HasValue)
            {
                contentPanel.Children.Add(CreatePityCard(
                    "五星保底进度",
                    countSinceLast5Star,
                    selectedCardPool.FiveStarPity.Value,
                    $"距离上一个五星已经垫了 {countSinceLast5Star} 发",
                    ColorHelper.FromArgb(255, 204, 156, 92)
                ));
            }

            if (selectedCardPool != null && selectedCardPool.FourStarPity.HasValue)
            {
                contentPanel.Children.Add(CreatePityCard(
                    "四星保底进度",
                    countSinceLast4Star,
                    selectedCardPool.FourStarPity.Value,
                    $"距离上一个四星已经抽了 {countSinceLast4Star} 发",
                    ColorHelper.FromArgb(255, 152, 120, 196)
                ));
            }

            var statCards = new List<Border>
            {
                CreateStatCard("五星数量", $"{rank5Records.Count}"),
                CreateStatCard("四星数量", $"{rank4Records.Count}"),
                CreateStatCard("五星获取率", rate5Star),
                CreateStatCard("四星获取率", rate4Star),
                CreateStatCard("五星平均", $"{averageDraws5Star} 抽"),
                CreateStatCard("四星平均", $"{averageDraws4Star} 抽")
            };

            if (selectedCardPool?.isPityEnable == true)
            {
                statCards.Add(CreateStatCard("五星Up数量", $"{upFiveStarRecords.Count}"));
                statCards.Add(CreateStatCard("五星常驻数量", $"{permanentFiveStarRecords.Count}"));
                statCards.Add(CreateStatCard("五星Up平均", $"{averageDraws5StarUp} 抽"));
                statCards.Add(CreateStatCard("五星Up歪率", upLossRate));
            }

            var statGrid = CreateStatGrid(statCards.ToArray());

            contentPanel.Children.Add(statGrid);

            scrollView.Content = contentPanel;
            Gacha_Panel.Children.Add(scrollView);
            Logging.Write("Finished displaying gacha details", 0);
        }

        private Border CreatePityCard(string title, int current, int maximum, string subtitle, Color accentColor)
        {
            var border = CreateDetailBorder(237);

            var root = new StackPanel
            {
                Spacing = 8
            };

            var titleRow = new Grid();

            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleText = new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14
            };

            var countText = new TextBlock
            {
                Text = $"{current}/{maximum}",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = new SolidColorBrush(accentColor)
            };

            Grid.SetColumn(titleText, 0);
            Grid.SetColumn(countText, 1);

            titleRow.Children.Add(titleText);
            titleRow.Children.Add(countText);

            var progressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = maximum,
                Value = Math.Min(current, maximum),
                Height = 8,
                CornerRadius = new CornerRadius(4)
            };

            var subtitleText = new TextBlock
            {
                Text = subtitle,
                FontSize = 12,
                Opacity = 0.65,
                TextWrapping = TextWrapping.WrapWholeWords
            };

            root.Children.Add(titleRow);
            root.Children.Add(progressBar);
            root.Children.Add(subtitleText);

            border.Child = root;
            return border;
        }

        private Border CreateOverviewCard(
            string poolName,
            int totalCount,
            int astriteCount,
            string latest5StarTime,
            string latest4StarTime)
        {
            var border = CreateDetailBorder(237);

            var root = new Grid
            {
                ColumnSpacing = 12
            };

            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var main = new StackPanel
            {
                Spacing = 6
            };

            main.Children.Add(new TextBlock
            {
                Text = poolName,
                FontSize = 12,
                Opacity = 0.65,
                TextWrapping = TextWrapping.WrapWholeWords
            });

            main.Children.Add(new TextBlock
            {
                Text = $"共 {totalCount} 抽",
                FontSize = 24,
                FontWeight = FontWeights.Bold
            });

            main.Children.Add(new TextBlock
            {
                Text = $"预计消耗 {astriteCount} 星声",
                FontSize = 12,
                Opacity = 0.7
            });

            main.Children.Add(new TextBlock
            {
                Text = $"最近五星：{latest5StarTime}",
                FontSize = 11,
                Opacity = 0.55,
                TextWrapping = TextWrapping.WrapWholeWords
            });

            main.Children.Add(new TextBlock
            {
                Text = $"最近四星：{latest4StarTime}",
                FontSize = 11,
                Opacity = 0.55,
                TextWrapping = TextWrapping.WrapWholeWords
            });

            Grid.SetColumn(main, 0);
            root.Children.Add(main);

            border.Child = root;
            return border;
        }

        private Border CreateStatCard(string title, string value)
        {
            return new Border
            {
                Padding = new Thickness(12, 10, 12, 10),
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontSize = 11,
                    Opacity = 0.65
                },
                new TextBlock
                {
                    Text = value,
                    FontSize = 17,
                    FontWeight = FontWeights.Bold,
                    TextWrapping = TextWrapping.WrapWholeWords
                }
            }
                }
            };
        }

        private Grid CreateStatGrid(params Border[] cards)
        {
            var grid = new Grid
            {
                ColumnSpacing = 8,
                RowSpacing = 8,
                Margin = new Thickness(0, 0, 0, 10)
            };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int i = 0; i < cards.Length && i < 10; i++)
            {
                Grid.SetColumn(cards[i], i % 2);
                Grid.SetRow(cards[i], i / 2);
                grid.Children.Add(cards[i]);
            }

            return grid;
        }

        private async Task EnsureWuwaAssetsCacheAsync()
        {
            if (_wuwaAssetsCache != null && DateTime.Now - _wuwaAssetsCacheLoadedAt < TimeSpan.FromHours(12))
            {
                return;
            }

            try
            {
                string cacheRoot = Path.Combine(AppDataController.GetDataPath("Cache"), "WuwaAssets");
                Directory.CreateDirectory(cacheRoot);
                string cacheFilePath = Path.Combine(cacheRoot, "wuwa-assets.json");

                bool shouldRequestRemote = !File.Exists(cacheFilePath) || DateTime.Now - File.GetLastWriteTime(cacheFilePath) > TimeSpan.FromHours(12);

                if (shouldRequestRemote)
                {
                    Logging.Write("Downloading wuwa assets index", 0);
                    string remoteJson = await _httpClient.GetStringAsync(WuwaAssetsApiUrl);
                    await File.WriteAllTextAsync(cacheFilePath, remoteJson);
                }

                string json = await File.ReadAllTextAsync(cacheFilePath);
                _wuwaAssetsCache = JsonConvert.DeserializeObject<WuwaAssetsCacheModel>(json) ?? new WuwaAssetsCacheModel();
                _wuwaAssetsCacheLoadedAt = DateTime.Now;
            }
            catch (Exception ex)
            {
                Logging.Write($"Load wuwa assets index failed: {ex.Message}", 1);

                try
                {
                    string cacheRoot = Path.Combine(AppDataController.GetDataPath("Cache"), "WuwaAssets");
                    string cacheFilePath = Path.Combine(cacheRoot, "wuwa-assets.json");
                    if (File.Exists(cacheFilePath))
                    {
                        string json = await File.ReadAllTextAsync(cacheFilePath);
                        _wuwaAssetsCache = JsonConvert.DeserializeObject<WuwaAssetsCacheModel>(json) ?? new WuwaAssetsCacheModel();
                        _wuwaAssetsCacheLoadedAt = DateTime.Now;
                    }
                }
                catch (Exception fallbackEx)
                {
                    Logging.Write($"Load local wuwa assets index failed: {fallbackEx.Message}", 1);
                    _wuwaAssetsCache ??= new WuwaAssetsCacheModel();
                }
            }
        }

        private WuwaAssetItem FindWuwaAssetItem(string resourceId, string resourceType)
        {
            if (_wuwaAssetsCache == null || string.IsNullOrWhiteSpace(resourceId))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(resourceType))
            {
                if (resourceType.Contains("武器") && _wuwaAssetsCache.weapon != null && _wuwaAssetsCache.weapon.TryGetValue(resourceId, out var weaponItemByType))
                {
                    return weaponItemByType;
                }

                if ((resourceType.Contains("角色") || resourceType.Contains("共鸣者")) && _wuwaAssetsCache.role != null && _wuwaAssetsCache.role.TryGetValue(resourceId, out var roleItemByType))
                {
                    return roleItemByType;
                }
            }

            if (_wuwaAssetsCache.role != null && _wuwaAssetsCache.role.TryGetValue(resourceId, out var roleItem))
            {
                return roleItem;
            }

            if (_wuwaAssetsCache.weapon != null && _wuwaAssetsCache.weapon.TryGetValue(resourceId, out var weaponItem))
            {
                return weaponItem;
            }

            return null;
        }

        private async Task<BitmapImage> GetGachaItemIconAsync(string resourceId, string name, string resourceType)
        {
            try
            {
                var assetItem = FindWuwaAssetItem(resourceId, resourceType);
                if (assetItem == null || string.IsNullOrWhiteSpace(assetItem.icon))
                {
                    Logging.Write($"Icon not found for {name}, resourceId: {resourceId}", 1);
                    return null;
                }

                string cacheRoot = Path.Combine(AppDataController.GetDataPath("Cache"), "WuwaAssets", "icons");
                Directory.CreateDirectory(cacheRoot);

                string extension = Path.GetExtension(new Uri(assetItem.icon).AbsolutePath);
                if (string.IsNullOrWhiteSpace(extension))
                {
                    extension = ".png";
                }

                string fileName = $"{resourceId}{extension}";
                string cacheFilePath = Path.Combine(cacheRoot, fileName);

                if (!File.Exists(cacheFilePath) || new FileInfo(cacheFilePath).Length == 0)
                {
                    Logging.Write($"Downloading gacha icon: {assetItem.icon}", 0);
                    byte[] bytes = await _httpClient.GetByteArrayAsync(assetItem.icon);
                    await File.WriteAllBytesAsync(cacheFilePath, bytes);
                }

                return new BitmapImage(new Uri(cacheFilePath));
            }
            catch (Exception ex)
            {
                Logging.Write($"Get icon failed for {name}: {ex.Message}", 1);
                return null;
            }
        }

        private Border CreateDetailBorder(double? width = null)
        {
            return new Border
            {
                Width = width ?? double.NaN,
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 10),
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                HorizontalAlignment = width.HasValue ? HorizontalAlignment.Left : HorizontalAlignment.Stretch
            };
        }
    }

    public class GachaInfoDisplayItem : INotifyPropertyChanged
    {
        private BitmapImage _iconSource;

        public string ResourceId { get; set; }
        public string ResourceType { get; set; }
        public string Name { get; set; }
        public string Count { get; set; }
        public string Pity { get; set; }
        public Visibility PityVisibility { get; set; }

        public BitmapImage IconSource
        {
            get => _iconSource;
            set
            {
                if (_iconSource == value)
                {
                    return;
                }

                _iconSource = value;
                OnPropertyChanged(nameof(IconSource));
                OnPropertyChanged(nameof(IsIconVisible));
                OnPropertyChanged(nameof(IsPlaceholderVisible));
            }
        }

        public bool IsIconVisible => IconSource != null;

        public bool IsPlaceholderVisible => IconSource == null;

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class WuwaAssetsCacheModel
    {
        public Dictionary<string, WuwaAssetItem> weapon { get; set; } = new Dictionary<string, WuwaAssetItem>();
        public Dictionary<string, WuwaAssetItem> role { get; set; } = new Dictionary<string, WuwaAssetItem>();
    }

    public class WuwaAssetItem
    {
        public int id { get; set; }
        public string name { get; set; }
        public int rank { get; set; }
        public int type { get; set; }
        public string icon { get; set; }
        public string acronym { get; set; }
        public bool isPreview { get; set; }
        public bool isNew { get; set; }
        public int priority { get; set; }
    }

    public class RankTypeToBackgroundColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var qualityLevel = value as int?;
            SolidColorBrush brush;

            switch (qualityLevel)
            {
                case 5:
                    // Gold color: #FFE2AC58
                    brush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xE2, 0xAC, 0x58));
                    break;
                case 4:
                    // Purple color: #FF7242B3
                    brush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x72, 0x42, 0xB3));
                    break;
                case 3:
                    // Dark Blue color: #FF3F5992
                    brush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x3F, 0x59, 0x92));
                    break;
                default:
                    brush = new SolidColorBrush(Colors.Transparent);
                    break;
            }
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException("Converting from a SolidColorBrush to a string is not supported.");
        }
    }


    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is bool boolValue && boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is Visibility visibility && visibility == Visibility.Visible;
        }
    }

    internal static class GachaCountColorThemeHelper
    {
        private const int GreenNode = 1;
        private const int GreenHoldNode = 30;
        private const int OrangeNode = 60;
        private const int PinkNode = 80;

        public static bool IsDarkTheme()
        {
            int dayNight = AppDataController.GetDayNight();

            if (dayNight == 1)
            {
                return false;
            }

            if (dayNight == 2)
            {
                return true;
            }

            var uiSettings = new Windows.UI.ViewManagement.UISettings();
            var foreground = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Foreground);

            return foreground.R > 128 && foreground.G > 128 && foreground.B > 128;
        }

        public static bool TryGetCount(object value, out int count)
        {
            if (value is int intValue)
            {
                count = intValue;
                return true;
            }

            if (value is string countString && int.TryParse(countString, out count))
            {
                return true;
            }

            count = 0;
            return false;
        }

        public static Color GetCountColor(int count, bool isProgressColor, bool isDarkTheme)
        {
            count = Math.Clamp(count, GreenNode, PinkNode);

            Color green;
            Color orange;
            Color pink;

            if (isDarkTheme)
            {
                if (isProgressColor)
                {
                    green = Color.FromArgb(120, 92, 156, 118);
                    orange = Color.FromArgb(120, 190, 132, 72);
                    pink = Color.FromArgb(120, 190, 86, 86);
                }
                else
                {
                    green = Color.FromArgb(255, 58, 108, 78);
                    orange = Color.FromArgb(255, 148, 98, 54);
                    pink = Color.FromArgb(255, 142, 68, 68);
                }
            }
            else
            {
                if (isProgressColor)
                {
                    green = Color.FromArgb(255, 55, 151, 101);
                    orange = Color.FromArgb(255, 195, 121, 54);
                    pink = Color.FromArgb(255, 194, 86, 112);
                }
                else
                {
                    green = Color.FromArgb(255, 42, 128, 88);
                    orange = Color.FromArgb(255, 174, 94, 28);
                    pink = Color.FromArgb(255, 174, 68, 96);
                }
            }

            if (count <= GreenHoldNode)
            {
                return green;
            }

            if (count <= OrangeNode)
            {
                double amount = (double)(count - GreenHoldNode) / (OrangeNode - GreenHoldNode);
                return LerpColor(green, orange, amount);
            }

            double pinkAmount = (double)(count - OrangeNode) / (PinkNode - OrangeNode);
            return LerpColor(orange, pink, pinkAmount);
        }

        public static Color LerpColor(Color from, Color to, double amount)
        {
            amount = Math.Clamp(amount, 0, 1);

            return Color.FromArgb(
                (byte)Math.Round(from.A + (to.A - from.A) * amount),
                (byte)Math.Round(from.R + (to.R - from.R) * amount),
                (byte)Math.Round(from.G + (to.G - from.G) * amount),
                (byte)Math.Round(from.B + (to.B - from.B) * amount)
            );
        }
    }

    public class CountToBackgroundColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isDarkTheme = GachaCountColorThemeHelper.IsDarkTheme();

            if (GachaCountColorThemeHelper.TryGetCount(value, out int count))
            {
                return new SolidColorBrush(GachaCountColorThemeHelper.GetCountColor(count, false, isDarkTheme));
            }

            return isDarkTheme
                ? new SolidColorBrush(Color.FromArgb(255, 70, 70, 70))
                : new SolidColorBrush(Color.FromArgb(255, 107, 114, 128));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class CountToProgressBackgroundColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isDarkTheme = GachaCountColorThemeHelper.IsDarkTheme();

            if (GachaCountColorThemeHelper.TryGetCount(value, out int count))
            {
                return new SolidColorBrush(GachaCountColorThemeHelper.GetCountColor(count, true, isDarkTheme));
            }

            return isDarkTheme
                ? new SolidColorBrush(Color.FromArgb(100, 130, 130, 130))
                : new SolidColorBrush(Color.FromArgb(255, 137, 144, 158));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class CountToProgressWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string countString && int.TryParse(countString, out int count))
            {
                double maxWidth = 259.0;
                double maxPity = 80.0;

                if (count < 0) count = 0;
                if (count > maxPity) count = (int)maxPity;

                return maxWidth * count / maxPity;
            }

            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
