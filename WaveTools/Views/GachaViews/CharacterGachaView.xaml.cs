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
using Microsoft.UI.Xaml.Controls;
using WaveTools.Depend;
using System.Linq;
using Windows.Storage;
using Spectre.Console;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using WaveTools.Views.ToolViews;


namespace WaveTools.Views.GachaViews
{

    public sealed partial class CharacterGachaView : Page
    {

        public CharacterGachaView()
        {
            this.InitializeComponent();
            Logging.Write("Switch to CharacterGachaView", 0);
            LoadData();

        }

        private async void LoadData()
        {
            string selectedUID = GachaView.GetSelectedUid();
            var folder = KnownFolders.DocumentsLibrary;
            var WaveToolsFolder = folder.GetFolderAsync("JSG-LLC\\WaveTools\\GachaRecords\\"+selectedUID).AsTask().GetAwaiter().GetResult();
            var settingsFile = WaveToolsFolder.GetFileAsync("GachaRecords_Character.ini").AsTask().GetAwaiter().GetResult();
            var GachaRecords = FileIO.ReadTextAsync(settingsFile).AsTask().GetAwaiter().GetResult();
            var records = await new GachaRecords().GetAllGachaRecordsAsync(null, GachaRecords);
            var groupedRecords = records.GroupBy(r => r.RankType).ToDictionary(g => g.Key, g => g.ToList());
            int RankType5 = records.TakeWhile(r => r.RankType != "5").Count();
            int RankType4 = records.TakeWhile(r => r.RankType != "4").Count();
            string uid = records.Select(r => r.Uid).FirstOrDefault();

            // 筛选出四星和五星的记录
            var rank4Records = records.Where(r => r.RankType == "4");
            var rank5Records = records.Where(r => r.RankType == "5");

            // 按名称进行分组并计算每个分组中的记录数量
            var rank4Grouped = rank4Records.GroupBy(r => r.Name).Select(g => new { Name = g.Key, Count = g.Count() });
            var rank5Grouped = rank5Records.GroupBy(r => r.Name).Select(g => new { Name = g.Key, Count = g.Count() });
            int rank5Count = 0;
            int i;
            int j = 0;
            bool NoEvent = false;
            // 输出五星记录
            var rank5TextBlock = new TextBlock {};
            var rank4TextBlock = new TextBlock {};
            rank5Records.Reverse();
            records.Reverse();
            foreach (var group in rank5Records)
            {
                for (i=j;i<records.Count;i++)
                {
                    //Logging.Write(i+ records[i].Name,0);
                    rank5Count++;
                    if (records[i].RankType == "5" && records[i].Name == group.Name)
                    {
                        Logging.Write("抽到5星:[[" + j+ "]]:"+ rank5Count + group.Name,0);
                        if (NoEvent) {
                            rank5TextBlock.Text += $"{group.Name}：用了{rank5Count}抽[大保底] \n";
                            NoEvent = false;
                        }
                        else {
                            rank5TextBlock.Text += $"{group.Name}：用了{rank5Count}抽 \n";
                            if (group.Name == "布洛妮娅" || group.Name == "杰帕德" || group.Name == "白露" || group.Name == "瓦尔特" || group.Name == "彦卿" || group.Name == "姬子" || group.Name == "克拉拉") NoEvent = true;
                        }
                        
                        rank5Count = 0;
                        j = i + 1;
                        break; //移动到下一个五星
                    }
                }
            }
            records.Reverse();
            var lines5 = rank5TextBlock.Text.Split("\n");
            var reversedLines5 = lines5.Reverse();
            rank5TextBlock.Text = string.Join("\n", reversedLines5);

            foreach (var group in rank4Grouped)
            {
                rank4TextBlock.Text += $"{group.Name} x{group.Count}, \n";
            }
            var lines4 = rank4TextBlock.Text.Split("\n");
            var reversedLines4 = lines4.Reverse();
            rank4TextBlock.Text = string.Join("\n", reversedLines4);
            MyStackPanel.Children.Clear();
            Gacha5Stars.Children.Clear();
            Gacha4Stars.Children.Clear();
            Gacha5Stars.Children.Add(rank5TextBlock);
            Gacha4Stars.Children.Add(rank4TextBlock);
            // 计算概率，基础概率应以小数形式传递
            double upcomingProbability5 = CalculateProbability(RankType5, 0.006, 89);
            double upcomingProbability4 = CalculateProbability(RankType4, 0.0255, 9);

            // 创建详情卡片
            Border borderInfo = new Border
            {
                Padding = new Thickness(10),
                Margin = new Thickness(0, 4, 0, 4), // 添加一些底部间距
                BorderBrush = new SolidColorBrush(Colors.Gray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };

            StackPanel stackPanelInfo = new StackPanel();

            stackPanelInfo.Children.Add(new TextBlock { Text = $"UID:" + uid });
            foreach (var group in groupedRecords)
            {
                var textBlock = new TextBlock
                {
                    Text = $"{group.Key}星: {group.Value.Count} (相同的有{group.Value.GroupBy(r => r.Name).Count()}个)"
                };
                stackPanelInfo.Children.Add(textBlock);
            }
            borderInfo.Child = stackPanelInfo;
            MyStackPanel.Children.Add(borderInfo);



            // 创建五星卡片
            Border borderFiveStar = new Border
            {
                Padding = new Thickness(10),
                Margin = new Thickness(0, 4, 0, 4), // 添加一些底部间距
                BorderBrush = new SolidColorBrush(Colors.Gray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };

            StackPanel stackPanelFiveStar = new StackPanel();
            stackPanelFiveStar.Children.Add(new TextBlock { Text = $"距离上一个五星已经抽了{RankType5}发" });
            ProgressBar progressBar5 = new ProgressBar
            {
                Minimum = 0,
                Maximum = 90,
                Value = RankType5,
                Height = 12
            };
            stackPanelFiveStar.Children.Add(progressBar5);
            stackPanelFiveStar.Children.Add(new TextBlock { Text = $"保底90发", FontSize = 12, Foreground = new SolidColorBrush(Colors.Gray) });
            stackPanelFiveStar.Children.Add(new TextBlock { Text = $"[Beta]下次五星的概率: {upcomingProbability5:F2}%", FontSize = 12, Foreground = new SolidColorBrush(Colors.Gray) });
            borderFiveStar.Child = stackPanelFiveStar;
            MyStackPanel.Children.Add(borderFiveStar);

            // 创建四星卡片
            Border borderFourStar = new Border
            {
                Padding = new Thickness(10),
                Margin = new Thickness(0, 4, 0, 4), // 添加一些底部间距
                BorderBrush = new SolidColorBrush(Colors.Gray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };
            StackPanel stackPanelFourStar = new StackPanel();
            stackPanelFourStar.Children.Add(new TextBlock { Text = $"距离上一个四星已经抽了{RankType4}发" });
            ProgressBar progressBar4 = new ProgressBar
            {
                Minimum = 0,
                Maximum = 10,
                Value = RankType4,
                Height = 12
            };
            stackPanelFourStar.Children.Add(progressBar4);
            stackPanelFourStar.Children.Add(new TextBlock { Text = $"保底10发", FontSize = 12, Foreground = new SolidColorBrush(Colors.Gray) });
            stackPanelFourStar.Children.Add(new TextBlock { Text = $"[Beta]下次四星的概率: {upcomingProbability4:F2}%", FontSize = 12, Foreground = new SolidColorBrush(Colors.Gray) });
            borderFourStar.Child = stackPanelFourStar;
            MyStackPanel.Children.Add(borderFourStar);

            MyListView.ItemsSource = records;
            //gacha_status.Text = "已加载本地缓存";

        }

        public double CalculateProbability(int drawsSinceLast, double baseProbability, int pityLimit)
        {
            if (drawsSinceLast >= pityLimit)
                return 100.0;

            double cumulativeProbability = 1.0;
            for (int i = 0; i < drawsSinceLast; i++)
            {
                cumulativeProbability *= (1 - baseProbability);
            }
            double probability = (1 - cumulativeProbability) * 100;
            return probability;
        }

    }

    public class RankTypeToBackgroundColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var rankType = value as string;
            SolidColorBrush brush;

            switch (rankType)
            {
                case "5":
                    // Gold color: #FFE2AC58
                    brush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xE2, 0xAC, 0x58));
                    break;
                case "4":
                    // Purple color: #FF7242B3
                    brush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x72, 0x42, 0xB3));
                    break;
                case "3":
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
}