using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json;
using WaveTools.Depend;
using WaveTools.Views.ToolViews;

namespace WaveTools.Views.GachaViews
{
    public sealed partial class TempGachaView : Page
    {
        public TempGachaView()
        {
            this.InitializeComponent();
            Logging.Write("Switch to TempGachaView", 0);
            LoadData();
        }

        private async void LoadData()
        {
            string selectedUID = GachaView.selectedUid;
            int selectedCardPoolId = GachaView.selectedCardPoolId;
            string recordsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JSG-LLC", "WaveTools", "GachaRecords");
            string filePath = Path.Combine(recordsDirectory, $"{selectedUID}.json");

            if (!File.Exists(filePath))
            {
                Console.WriteLine("找不到UID的抽卡记录文件");
                return;
            }

            string jsonContent = await File.ReadAllTextAsync(filePath);
            var gachaData = JsonConvert.DeserializeObject<GachaData>(jsonContent);
            var records = gachaData.List.Where(pool => pool.CardPoolId == selectedCardPoolId).SelectMany(pool => pool.Records).ToList();

            // 筛选出四星和五星的记录
            var rank4Records = records.Where(r => r.QualityLevel == 4).ToList();
            var rank5Records = records.Where(r => r.QualityLevel == 5).ToList();

            // 按名称进行分组并计算每个分组中的记录数量
            var rank4Grouped = rank4Records.GroupBy(r => r.Name).Select(g => new GroupedRecord { Name = g.Key, Count = g.Count() }).ToList();
            var rank5Grouped = rank5Records.GroupBy(r => r.Name).Select(g => new GroupedRecord { Name = g.Key, Count = g.Count() }).ToList();

            // 获取卡池信息
            var cardPoolInfo = await GetCardPoolInfo();

            if (cardPoolInfo == null || cardPoolInfo.CardPools == null)
            {
                Console.WriteLine("无法获取卡池信息或卡池列表为空");
                return;
            }

            // 显示抽卡记录
            DisplayGachaRecords(rank4Grouped, rank5Grouped, records);

            // 显示抽卡详情
            DisplayGachaDetails(gachaData, rank4Records, rank5Records, selectedCardPoolId, cardPoolInfo);
        }

        private async Task<CardPoolInfo> GetCardPoolInfo()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var response = await client.GetStringAsync("https://wavetools.jamsg.cn/api/cardPoolRule");
                    return JsonConvert.DeserializeObject<CardPoolInfo>(response);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取卡池信息时发生错误: {ex.Message}");
                throw;
            }
        }

        private void DisplayGachaRecords(List<GroupedRecord> rank4Grouped, List<GroupedRecord> rank5Grouped, List<GachaRecord> records)
        {
            Gacha5Stars.Children.Clear();
            Gacha4Stars.Children.Clear();

            var rank5TextBlock = new TextBlock();
            var rank4TextBlock = new TextBlock();

            foreach (var group in rank5Grouped)
            {
                rank5TextBlock.Text += $"{group.Name} x{group.Count}\n";
            }
            foreach (var group in rank4Grouped)
            {
                rank4TextBlock.Text += $"{group.Name} x{group.Count}\n";
            }

            Gacha5Stars.Children.Add(rank5TextBlock);
            Gacha4Stars.Children.Add(rank4TextBlock);

            MyListView.ItemsSource = records;
        }

        private void DisplayGachaDetails(GachaData gachaData, List<GachaRecord> rank4Records, List<GachaRecord> rank5Records, int selectedCardPoolId, CardPoolInfo cardPoolInfo)
        {
            MyStackPanel.Children.Clear();

            var selectedRecords = gachaData.List
                .Where(pool => pool.CardPoolId == selectedCardPoolId)
                .SelectMany(pool => pool.Records)
                .OrderByDescending(r => r.Time) // 确保按时间逆序排列
                .ToList();

            int countSinceLast5Star = 0;
            int countSinceLast4Star = 0;
            bool foundLast5Star = false;
            bool foundLast4Star = false;

            foreach (var record in selectedRecords)
            {
                if (!foundLast5Star)
                {
                    if (record.QualityLevel == 5)
                    {
                        foundLast5Star = true;
                    }
                    else
                    {
                        countSinceLast5Star++;
                    }
                }

                if (!foundLast4Star)
                {
                    if (record.QualityLevel == 4)
                    {
                        foundLast4Star = true;
                    }
                    else
                    {
                        countSinceLast4Star++;
                    }
                }

                if (foundLast5Star && foundLast4Star)
                {
                    break;
                }
            }

            // 创建详情卡片
            var borderInfo = CreateDetailBorder();
            var stackPanelInfo = new StackPanel();

            stackPanelInfo.Children.Add(new TextBlock { Text = $"UID: {gachaData.Info.Uid}" });

            var groupedRecords = selectedRecords
                .GroupBy(r => r.QualityLevel)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var group in groupedRecords)
            {
                var distinctCount = group.Value.GroupBy(r => r.Name).Count();
                stackPanelInfo.Children.Add(new TextBlock { Text = $"{group.Key}星: {group.Value.Count} (相同的有{distinctCount}个)" });
            }

            borderInfo.Child = stackPanelInfo;
            MyStackPanel.Children.Add(borderInfo);

            var selectedCardPool = cardPoolInfo.CardPools.FirstOrDefault(cp => cp.CardPoolId == selectedCardPoolId);

            if (selectedCardPool != null)
            {
                if (selectedCardPool.FiveStarPity.HasValue)
                {
                    // 创建五星卡片
                    var borderFiveStar = CreateDetailBorder();
                    var stackPanelFiveStar = new StackPanel();

                    stackPanelFiveStar.Children.Add(new TextBlock { Text = $"距离上一个五星已经抽了{countSinceLast5Star}发" });
                    var progressBar5 = CreateProgressBar(countSinceLast5Star, selectedCardPool.FiveStarPity.Value);
                    stackPanelFiveStar.Children.Add(progressBar5);
                    stackPanelFiveStar.Children.Add(new TextBlock { Text = $"保底{selectedCardPool.FiveStarPity.Value}发", FontSize = 12, Foreground = new SolidColorBrush(Colors.Gray) });

                    borderFiveStar.Child = stackPanelFiveStar;
                    MyStackPanel.Children.Add(borderFiveStar);
                }

                if (selectedCardPool.FourStarPity.HasValue)
                {
                    // 创建四星卡片
                    var borderFourStar = CreateDetailBorder();
                    var stackPanelFourStar = new StackPanel();

                    stackPanelFourStar.Children.Add(new TextBlock { Text = $"距离上一个四星已经抽了{countSinceLast4Star}发" });
                    var progressBar4 = CreateProgressBar(countSinceLast4Star, selectedCardPool.FourStarPity.Value);
                    stackPanelFourStar.Children.Add(progressBar4);
                    stackPanelFourStar.Children.Add(new TextBlock { Text = $"保底{selectedCardPool.FourStarPity.Value}发", FontSize = 12, Foreground = new SolidColorBrush(Colors.Gray) });

                    borderFourStar.Child = stackPanelFourStar;
                    MyStackPanel.Children.Add(borderFourStar);
                }
            }
        }



        private Border CreateDetailBorder()
        {
            return new Border
            {
                Padding = new Thickness(10),
                Margin = new Thickness(0, 4, 0, 4),
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };
        }

        private ProgressBar CreateProgressBar(int value, int maximum)
        {
            return new ProgressBar
            {
                Minimum = 0,
                Maximum = maximum,
                Value = value,
                Height = 12
            };
        }
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

    public class GroupedRecord
    {
        public string Name { get; set; }
        public int Count { get; set; }
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

    public class CardPool
    {
        public int CardPoolId { get; set; }
        public string CardPoolType { get; set; }
        public int? FiveStarPity { get; set; }
        public int? FourStarPity { get; set; }
    }

    public class CardPoolInfo
    {
        public List<CardPool> CardPools { get; set; }
    }
}
