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
using Microsoft.UI.Xaml.Markup;
using Newtonsoft.Json;
using WaveTools.Depend;
using WaveTools.Views.ToolViews;
using Windows.UI;
using static WaveTools.App;

namespace WaveTools.Views.GachaViews
{
    public sealed partial class TempGachaView : Page
    {
        private const double DetailsPanelWidth = 237;
        private const double StatCardSpacing = 8;
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
            var selectedCardPool = GachaView.cardPoolInfo?.CardPools?.FirstOrDefault(cp => cp.CardPoolId == selectedCardPoolId);

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
                    Pity = selectedCardPool?.GetFiveStarUpLabel(record) ?? string.Empty,
                    PityVisibility = selectedCardPool?.IsFiveStarLost(record) == true
                        ? Visibility.Visible
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

                bool hasMissingIcons = items.Any(item =>
                {
                    var assetItem = FindWuwaAssetItem(item.ResourceId, item.ResourceType);
                    return assetItem == null || string.IsNullOrWhiteSpace(assetItem.icon);
                });

                if (hasMissingIcons)
                {
                    Logging.Write("Gacha icons missing from cached index, refreshing wuwa assets index", 0);
                    await EnsureWuwaAssetsCacheAsync(true);

                    if (loadToken != _loadDataToken)
                    {
                        return;
                    }
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


        private List<int> CalculateIntervals(List<GachaModel.GachaRecord> records, int qualityLevel, Func<GachaModel.GachaRecord, bool> predicate = null)
        {
            var intervals = new List<int>();
            int countSinceLastStar = 0;

            // 倒序遍历记录
            foreach (var record in records.AsEnumerable().Reverse())
            {
                countSinceLastStar++; // 每次迭代都递增计数器

                if (record.QualityLevel == qualityLevel && (predicate == null || predicate(record)))
                {
                    intervals.Add(countSinceLastStar); // 将计数器的值添加到间隔列表中
                    countSinceLastStar = 0; // 重置计数器
                }
            }

            return intervals;
        }

        private void DisplayGachaDetails(GachaModel.GachaData gachaData, List<GachaModel.GachaRecord> rank4Records, List<GachaModel.GachaRecord> rank5Records, int selectedCardPoolId, GachaModel.CardPoolInfo cardPoolInfo)
        {
            Logging.Write("Displaying gacha details", 0);
            Gacha_Panel.Children.Clear();
            var contentPanel = new StackPanel
            {
                Width = DetailsPanelWidth,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var selectedRecords = gachaData.List
                .Where(pool => pool.CardPoolId == selectedCardPoolId)
                .SelectMany(pool => pool.Records)
                .OrderByDescending(r => GetRecordDate(r))
                .ThenByDescending(r => r.Id, StringComparer.Ordinal)
                .ToList();

            Logging.Write($"Total selected records: {selectedRecords.Count}", 0);
            var selectedCardPool = cardPoolInfo?.CardPools?.FirstOrDefault(cp => cp.CardPoolId == selectedCardPoolId);

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
            // 歪出的五星不重置计数，累计到下一个UP；未完成的垫抽不计入平均。
            var fiveStarUpIntervals = CalculateIntervals(selectedRecords, 5, record => selectedCardPool?.IsFiveStarUp(record) == true);
            bool supportsFiveStarUp = selectedCardPool?.HasFiveStarUp == true;

            // 计算平均值
            string averageDraws4Star = fourStarIntervals.Count > 0 ? (fourStarIntervals.Average()).ToString("F2") : "—";
            string averageDraws5Star = fiveStarIntervals.Count > 0 ? (fiveStarIntervals.Average()).ToString("F2") : "—";
            string fiveStarUpCount = supportsFiveStarUp ? fiveStarUpIntervals.Count.ToString() : "不适用";
            string averageDraws5StarUp = supportsFiveStarUp
                ? (fiveStarUpIntervals.Count > 0 ? fiveStarUpIntervals.Average().ToString("F2") + " 抽" : "—")
                : "不适用";

            Gacha_UID.Text = gachaData.Info.Uid;
            GachaRecords_Count.Text = "共" + selectedRecords.Count() + "抽";
            GachaInfo_SinceLast5Star.Text = $"垫了{countSinceLast5Star}发";

            string rate4Star = rank4Records.Count > 0
                ? (rank4Records.Count / (double)selectedRecords.Count * 100).ToString("F2") + "%"
                : "0.00%";

            string rate5Star = rank5Records.Count > 0
                ? (rank5Records.Count / (double)selectedRecords.Count * 100).ToString("F2") + "%"
                : "0.00%";

            string latest5StarTime = selectedRecords.FirstOrDefault(record => record.QualityLevel == 5)?.Time ?? "暂无记录";
            string latest4StarTime = selectedRecords.FirstOrDefault(record => record.QualityLevel == 4)?.Time ?? "暂无记录";

            string poolName = selectedCardPool?.CardPoolType ?? $"卡池 {selectedCardPoolId}";

            contentPanel.Children.Add(CreateOverviewCard(
                poolName,
                selectedRecords.Count,
                selectedRecords.Count * 160,
                latest5StarTime,
                latest4StarTime
            ));
            contentPanel.Children.Add(CreateRarityCompositionCard(selectedRecords));

            if (selectedCardPool?.HasFiveStarPity == true)
            {
                contentPanel.Children.Add(CreatePityCard(
                    "五星保底进度",
                    countSinceLast5Star,
                    selectedCardPool.FiveStarPity.Value,
                    ColorHelper.FromArgb(255, 204, 156, 92)
                ));
            }

            if (selectedCardPool?.HasFourStarPity == true)
            {
                contentPanel.Children.Add(CreatePityCard(
                    "四星保底进度",
                    countSinceLast4Star,
                    selectedCardPool.FourStarPity.Value,
                    ColorHelper.FromArgb(255, 152, 120, 196)
                ));
            }

            var statGrid = CreateStatGrid(
                CreateStatCard("五星数量", $"{rank5Records.Count}"),
                CreateStatCard("四星数量", $"{rank4Records.Count}"),
                CreateStatCard("五星获取率", rate5Star),
                CreateStatCard("四星获取率", rate4Star),
                CreateStatCard("五星平均", $"{averageDraws5Star} 抽"),
                CreateStatCard("四星平均", $"{averageDraws4Star} 抽"),
                CreateStatCard("五星UP数量", fiveStarUpCount),
                CreateStatCard("五星UP平均抽数", averageDraws5StarUp)
            );

            contentPanel.Children.Add(statGrid);

            var fiveStarDraws = GetFiveStarDraws(selectedRecords);
            if (supportsFiveStarUp)
            {
                contentPanel.Children.Add(CreateUpPerformanceCard(fiveStarDraws, selectedCardPool));
            }
            contentPanel.Children.Add(CreateFiveStarTrendCard(fiveStarDraws, selectedCardPool));
            contentPanel.Children.Add(CreateDrawDistributionCard(fiveStarIntervals));
            contentPanel.Children.Add(CreateMonthlyActivityCard(selectedRecords));

            Gacha_Panel.Children.Add(contentPanel);
            Logging.Write("Finished displaying gacha details", 0);
        }

        private Border CreatePityCard(string title, int current, int maximum, Color accentColor)
        {
            var border = CreateDetailBorder();

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
                Foreground = new SolidColorBrush(accentColor),
                Height = 8,
                CornerRadius = new CornerRadius(4)
            };

            root.Children.Add(titleRow);
            root.Children.Add(progressBar);

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
            var border = CreateDetailBorder();

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
                Text = $"唤取价值 {astriteCount:N0} 星声",
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
            ToolTipService.SetToolTip(root, "160 星声/抽，非实际支出");

            border.Child = root;
            return border;
        }

        private Border CreateStatCard(string title, string value)
        {
            var border = CreateDetailBorder((DetailsPanelWidth - StatCardSpacing) / 2);
            border.Padding = new Thickness(12, 10, 12, 10);
            border.Margin = new Thickness(0);
            border.CornerRadius = new CornerRadius(10);
            border.Child = new StackPanel
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
            };
            return border;
        }

        private Grid CreateStatGrid(params Border[] cards)
        {
            var grid = new Grid
            {
                Width = DetailsPanelWidth,
                HorizontalAlignment = HorizontalAlignment.Left,
                ColumnSpacing = StatCardSpacing,
                RowSpacing = 8,
                Margin = new Thickness(0, 0, 0, 10)
            };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (int row = 0; row < (cards.Length + 1) / 2; row++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            for (int i = 0; i < cards.Length; i++)
            {
                Grid.SetColumn(cards[i], i % 2);
                Grid.SetRow(cards[i], i / 2);
                grid.Children.Add(cards[i]);
            }

            return grid;
        }

        private static readonly Color AnalyticsGold = GachaRarityColors.FiveStar;
        private static readonly Color AnalyticsPurple = GachaRarityColors.FourStar;
        private static readonly Color AnalyticsMint = ColorHelper.FromArgb(255, 65, 173, 147);
        private static readonly Color AnalyticsRose = ColorHelper.FromArgb(255, 214, 120, 139);
        private static readonly Color AnalyticsMutedPurple = ColorHelper.FromArgb(255, 146, 137, 163);
        private static readonly Color AnalyticsBlue = GachaRarityColors.ThreeStar;

        private sealed class FiveStarDraw
        {
            public GachaModel.GachaRecord Record { get; set; }
            public int Draws { get; set; }
        }

        private static DateTime? GetRecordDate(GachaModel.GachaRecord record)
        {
            return DateTime.TryParse(record.Time, out DateTime date) ? date : (DateTime?)null;
        }

        private static List<FiveStarDraw> GetFiveStarDraws(List<GachaModel.GachaRecord> records)
        {
            var result = new List<FiveStarDraw>();
            int draws = 0;
            foreach (var record in records.AsEnumerable().Reverse())
            {
                draws++;
                if (record.QualityLevel == 5)
                {
                    result.Add(new FiveStarDraw { Record = record, Draws = draws });
                    draws = 0;
                }
            }
            return result;
        }

        private static TextBlock CreateAnalyticsText(string text, double size = 12, double opacity = 1)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                Opacity = opacity,
                TextWrapping = TextWrapping.WrapWholeWords
            };
        }

        private Border CreateAnalyticsCard(string title, out StackPanel content)
        {
            var border = CreateDetailBorder();
            content = new StackPanel { Spacing = 10 };
            var titleText = CreateAnalyticsText(title, 14);
            titleText.FontWeight = FontWeights.SemiBold;
            content.Children.Add(titleText);
            border.Child = content;
            return border;
        }

        private static Border CreateAnalyticsTrack(double height = 7)
        {
            var track = (Border)XamlReader.Load(
                "<Border xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
                "Background=\"{ThemeResource CardBackgroundFillColorSecondaryBrush}\" CornerRadius=\"4\"/>"
            );
            track.Height = height;
            return track;
        }

        private static Border CreateShareBar(params (int Count, Color Tint, string Hint)[] segments)
        {
            var track = CreateAnalyticsTrack(9);
            var grid = new Grid { ColumnSpacing = 2 };
            foreach (var segment in segments.Where(segment => segment.Count > 0))
            {
                int column = grid.ColumnDefinitions.Count;
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(segment.Count, GridUnitType.Star) });
                var bar = new Border { Background = new SolidColorBrush(segment.Tint), CornerRadius = new CornerRadius(3) };
                ToolTipService.SetToolTip(bar, segment.Hint);
                Grid.SetColumn(bar, column);
                grid.Children.Add(bar);
            }
            track.Child = grid;
            return track;
        }

        private static Grid CreateAnalyticsLabelRow(string label, string value)
        {
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(CreateAnalyticsText(label, 11, 0.75));
            var valueText = CreateAnalyticsText(value, 11);
            valueText.FontWeight = FontWeights.SemiBold;
            Grid.SetColumn(valueText, 1);
            row.Children.Add(valueText);
            return row;
        }

        private Border CreateRarityCompositionCard(List<GachaModel.GachaRecord> records)
        {
            var card = CreateAnalyticsCard("星级构成", out var content);
            var groups = new[]
            {
                (Rank: 5, Label: "五星", Tint: AnalyticsGold),
                (Rank: 4, Label: "四星", Tint: AnalyticsPurple),
                (Rank: 3, Label: "三星及其他", Tint: AnalyticsBlue)
            };
            var counts = groups.Select(group => group.Rank == 3
                ? records.Count(record => record.QualityLevel != 5 && record.QualityLevel != 4)
                : records.Count(record => record.QualityLevel == group.Rank)).ToArray();
            content.Children.Add(CreateShareBar(groups.Select((group, index) =>
                (counts[index], group.Tint, $"{group.Label}：{counts[index]} 抽")).ToArray()));
            for (int i = 0; i < groups.Length; i++)
            {
                double percentage = records.Count > 0 ? counts[i] * 100.0 / records.Count : 0;
                var row = CreateAnalyticsLabelRow(groups[i].Label, $"{counts[i]} · {percentage:F1}%");
                row.Children.Insert(0, new Border
                {
                    Width = 3,
                    Background = new SolidColorBrush(groups[i].Tint),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CornerRadius = new CornerRadius(2)
                });
                ((TextBlock)row.Children[1]).Margin = new Thickness(9, 0, 0, 0);
                content.Children.Add(row);
            }
            return card;
        }

        private Border CreateUpPerformanceCard(List<FiveStarDraw> fiveStars, GachaModel.CardPool pool)
        {
            int upCount = fiveStars.Count(draw => pool.IsFiveStarUp(draw.Record));
            int offCount = fiveStars.Count - upCount;
            var card = CreateAnalyticsCard("五星 UP 占比", out var content);
            var percentage = CreateAnalyticsText(fiveStars.Count > 0 ? $"{upCount * 100.0 / fiveStars.Count:F1}%" : "暂无五星", 28);
            percentage.FontWeight = FontWeights.Bold;
            content.Children.Add(percentage);
            content.Children.Add(CreateShareBar(
                (upCount, AnalyticsGold, $"UP 五星：{upCount} 个"),
                (offCount, AnalyticsMutedPurple, $"歪了：{offCount} 个")));
            content.Children.Add(CreateAnalyticsLabelRow("UP / 歪了", $"{upCount} / {offCount}"));
            int completedDraws = 0;
            int drawsSinceUp = 0;
            foreach (var draw in fiveStars)
            {
                drawsSinceUp += draw.Draws;
                if (pool.IsFiveStarUp(draw.Record))
                {
                    completedDraws += drawsSinceUp;
                    drawsSinceUp = 0;
                }
            }
            content.Children.Add(CreateAnalyticsLabelRow("UP 用抽", $"{completedDraws} 抽"));
            return card;
        }



        private Border CreateFiveStarTrendCard(List<FiveStarDraw> fiveStars, GachaModel.CardPool pool)
        {
            var card = CreateAnalyticsCard("最近 8 次出金", out var content);
            var recent = fiveStars.TakeLast(8).Reverse().ToList();
            if (recent.Count == 0)
            {
                content.Children.Add(CreateAnalyticsText("暂无记录", 12, 0.65));
                return card;
            }

            int scale = Math.Max(1, Math.Max(pool?.HasFiveStarPity == true ? pool.FiveStarPity.Value : 0, recent.Max(draw => draw.Draws)));
            var chart = new Grid { ColumnSpacing = 5 };
            bool supportsUp = pool?.HasFiveStarUp == true;
            const double plotHeight = 76;
            const double markerSpace = 12;
            for (int i = 0; i < recent.Count; i++)
            {
                var draw = recent[i];
                chart.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var column = new StackPanel { Spacing = 5, MaxWidth = 28, HorizontalAlignment = HorizontalAlignment.Center };
                var count = CreateAnalyticsText(draw.Draws.ToString(), 10);
                count.HorizontalAlignment = HorizontalAlignment.Center;
                column.Children.Add(count);
                double barHeight = Math.Max(3, draw.Draws * plotHeight / scale);
                var plot = new Grid { Width = 10, Height = plotHeight + markerSpace, HorizontalAlignment = HorizontalAlignment.Center };
                var background = CreateAnalyticsTrack(plotHeight);
                background.VerticalAlignment = VerticalAlignment.Bottom;
                background.Opacity = 0.7;
                plot.Children.Add(background);
                bool isLost = supportsUp && pool.IsFiveStarLost(draw.Record);
                Color tint = isLost ? AnalyticsMutedPurple : AnalyticsGold;
                plot.Children.Add(new Border
                {
                    Height = barHeight,
                    Background = new LinearGradientBrush
                    {
                        StartPoint = new Windows.Foundation.Point(0, 1),
                        EndPoint = new Windows.Foundation.Point(0, 0),
                        GradientStops =
                        {
                            new GradientStop { Color = ColorHelper.FromArgb(140, tint.R, tint.G, tint.B), Offset = 0 },
                            new GradientStop { Color = tint, Offset = 1 }
                        }
                    },
                    VerticalAlignment = VerticalAlignment.Bottom,
                    CornerRadius = new CornerRadius(4)
                });
                if (isLost)
                {
                    var lostMark = new Grid
                    {
                        Width = 8,
                        Height = 8,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(0, plotHeight - barHeight, 0, 0),
                        IsHitTestVisible = false
                    };
                    var stroke = new SolidColorBrush(AnalyticsMutedPurple);
                    lostMark.Children.Add(new Microsoft.UI.Xaml.Shapes.Line
                    {
                        X1 = 1, Y1 = 1, X2 = 7, Y2 = 7,
                        Stroke = stroke, StrokeThickness = 1.5
                    });
                    lostMark.Children.Add(new Microsoft.UI.Xaml.Shapes.Line
                    {
                        X1 = 7, Y1 = 1, X2 = 1, Y2 = 7,
                        Stroke = stroke, StrokeThickness = 1.5
                    });
                    plot.Children.Add(lostMark);
                }
                column.Children.Add(plot);
                if (i == 0)
                {
                    var latestLabel = CreateAnalyticsText("最新", 10, 0.65);
                    latestLabel.HorizontalAlignment = HorizontalAlignment.Center;
                    column.Children.Add(latestLabel);
                }
                ToolTipService.SetToolTip(column, $"{draw.Record.Name} · {draw.Draws} 抽\n{draw.Record.Time}");
                Grid.SetColumn(column, i);
                chart.Children.Add(column);
            }
            content.Children.Add(chart);
            if (supportsUp)
            {
                var legend = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
                foreach (var item in new[] { (Label: "UP", Tint: AnalyticsGold), (Label: "歪了", Tint: AnalyticsMutedPurple) })
                {
                    var entry = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
                    entry.Children.Add(new Border
                    {
                        Width = 6,
                        Height = 6,
                        CornerRadius = new CornerRadius(3),
                        Background = new SolidColorBrush(item.Tint),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    entry.Children.Add(CreateAnalyticsText(item.Label, 11, 0.65));
                    legend.Children.Add(entry);
                }
                content.Children.Add(legend);
            }
            content.Children.Add(CreateAnalyticsLabelRow("平均", $"{recent.Average(draw => draw.Draws):F2} 抽"));
            return card;
        }

        private Border CreateDrawDistributionCard(List<int> intervals)
        {
            var card = CreateAnalyticsCard("出金抽数分布", out var content);
            if (intervals.Count == 0)
            {
                content.Children.Add(CreateAnalyticsText("暂无记录", 12, 0.65));
                return card;
            }

            var sorted = intervals.OrderBy(value => value).ToList();
            double median = (sorted[(sorted.Count - 1) / 2] + (double)sorted[sorted.Count / 2]) / 2;
            content.Children.Add(CreateAnalyticsLabelRow("最快 / 最慢", $"{sorted.First()} / {sorted.Last()} 抽"));
            content.Children.Add(CreateAnalyticsLabelRow("中位抽数", $"{median:F1} 抽"));
            var ranges = new[]
            {
                (Min: 1, Max: 20, Label: "1–20 抽", Tint: AnalyticsMint),
                (Min: 21, Max: 40, Label: "21–40 抽", Tint: AnalyticsBlue),
                (Min: 41, Max: 60, Label: "41–60 抽", Tint: AnalyticsPurple),
                (Min: 61, Max: 80, Label: "61–80 抽", Tint: AnalyticsGold),
                (Min: 81, Max: int.MaxValue, Label: "80 抽以上", Tint: AnalyticsRose)
            };
            foreach (var range in ranges)
            {
                int count = intervals.Count(value => value >= range.Min && value <= range.Max);
                if (range.Min == 81 && count == 0)
                {
                    continue;
                }
                var row = new StackPanel { Spacing = 5 };
                row.Children.Add(CreateAnalyticsLabelRow(range.Label, $"{count} 次 · {count * 100.0 / intervals.Count:F0}%"));
                row.Children.Add(CreateShareBar((count, range.Tint, $"{count} 次"),
                    (intervals.Count - count, Colors.Transparent, "其他区间")));
                content.Children.Add(row);
            }
            return card;
        }

        private Border CreateMonthlyActivityCard(List<GachaModel.GachaRecord> records)
        {
            var dates = records.Select(GetRecordDate).Where(date => date.HasValue).Select(date => date.Value).ToList();
            var card = CreateAnalyticsCard("近 6 个月抽卡量", out var content);
            if (dates.Count == 0)
            {
                content.Children.Add(CreateAnalyticsText("暂无日期", 12, 0.65));
                return card;
            }

            DateTime latest = dates.Max();
            DateTime latestMonth = new DateTime(latest.Year, latest.Month, 1);
            var months = Enumerable.Range(0, 6).Select(index => latestMonth.AddMonths(-index)).ToList();
            var counts = months.Select(month => dates.Count(date => date.Year == month.Year && date.Month == month.Month)).ToList();
            int maximum = Math.Max(1, counts.Max());
            for (int i = 0; i < months.Count; i++)
            {
                var row = new StackPanel { Spacing = 5 };
                row.Children.Add(CreateAnalyticsLabelRow(months[i].ToString("yyyy.MM"), $"{counts[i]} 抽"));
                row.Children.Add(CreateShareBar((counts[i], AnalyticsBlue, $"{months[i]:yyyy.MM}：{counts[i]} 抽"),
                    (maximum - counts[i], Colors.Transparent, "")));
                content.Children.Add(row);
            }
            content.Children.Add(CreateAnalyticsLabelRow("六个月累计", $"{counts.Sum():N0} 抽"));
            content.Children.Add(CreateAnalyticsText($"记录范围 {dates.Min():yyyy.MM.dd} — {latest:yyyy.MM.dd}", 11, 0.6));
            if (dates.Count != records.Count)
            {
                content.Children.Add(CreateAnalyticsLabelRow("无效日期", $"{records.Count - dates.Count} 条"));
            }
            return card;
        }



        private async Task EnsureWuwaAssetsCacheAsync(bool forceRefresh = false)
        {
            if (!forceRefresh && _wuwaAssetsCache != null && DateTime.Now - _wuwaAssetsCacheLoadedAt < TimeSpan.FromHours(12))
            {
                return;
            }

            try
            {
                string cacheRoot = Path.Combine(AppDataController.GetDataPath("Cache"), "WuwaAssets");
                Directory.CreateDirectory(cacheRoot);
                string cacheFilePath = Path.Combine(cacheRoot, "wuwa-assets.json");

                bool shouldRequestRemote = forceRefresh || !File.Exists(cacheFilePath) || DateTime.Now - File.GetLastWriteTime(cacheFilePath) > TimeSpan.FromHours(12);

                if (shouldRequestRemote)
                {
                    Logging.Write("Downloading wuwa assets index", 0);
                    string remoteJson = await _httpClient.GetStringAsync(WuwaAssetsApiUrl);
                    var remoteCache = JsonConvert.DeserializeObject<WuwaAssetsCacheModel>(remoteJson);
                    if (remoteCache == null ||
                        ((remoteCache.role?.Count ?? 0) == 0 && (remoteCache.weapon?.Count ?? 0) == 0))
                    {
                        throw new InvalidDataException("资源索引中没有角色或武器数据。");
                    }

                    await File.WriteAllTextAsync(cacheFilePath, remoteJson);
                    _wuwaAssetsCache = remoteCache;
                    _wuwaAssetsCacheLoadedAt = DateTime.Now;
                    return;
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
            var border = (Border)XamlReader.Load(
                "<Border xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
                "Background=\"{ThemeResource CardBackgroundFillColorDefaultBrush}\" " +
                "BorderBrush=\"{ThemeResource CardStrokeColorDefaultBrush}\" " +
                "BorderThickness=\"1\" CornerRadius=\"12\" Padding=\"14\" Margin=\"0,0,0,10\"/>"
            );
            border.Width = width ?? DetailsPanelWidth;
            border.HorizontalAlignment = HorizontalAlignment.Left;
            return border;
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

    internal static class GachaRarityColors
    {
        public static readonly Color FiveStar = ColorHelper.FromArgb(255, 211, 161, 75);
        public static readonly Color FourStar = ColorHelper.FromArgb(255, 160, 132, 217);
        public static readonly Color ThreeStar = ColorHelper.FromArgb(255, 103, 159, 213);

        public static Color GetRecordBackground(int qualityLevel) => qualityLevel switch
        {
            5 => ColorHelper.FromArgb(255, 166, 125, 58),
            4 => ColorHelper.FromArgb(255, 120, 98, 163),
            3 => ColorHelper.FromArgb(255, 74, 113, 155),
            _ => Colors.Transparent
        };
    }

    public class RankTypeToBackgroundColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return new SolidColorBrush(value is int qualityLevel
                ? GachaRarityColors.GetRecordBackground(qualityLevel)
                : Colors.Transparent);
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
