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
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using Microsoft.UI.Xaml.Media.Animation;
using WaveTools.Depend;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Input;
using WaveTools.Views.NotifyViews;
using System.IO;
using static WaveTools.App;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using Newtonsoft.Json;
using Windows.Services.Maps.Guidance;
using SRTools.Depend;

namespace WaveTools.Views
{
    public sealed partial class MainView : Page
    {
        private static readonly HttpClient httpClient = new HttpClient();
        public ObservableCollection<string> Pictures { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> PicturesClick { get; } = new ObservableCollection<string>();
        static Dictionary<string, BitmapImage> imageCache = new Dictionary<string, BitmapImage>();

        private string _url;
        List<String> list = new List<String>();
        GetNotify getNotify = new GetNotify();

        public MainView()
        {
            this.InitializeComponent();
            Logging.Write("Switch to MainView", 0);
            Loaded += MainView_Loaded;  // 订阅 Loaded 事件
        }

        private async void MainView_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadBackgroundAsync();
            await LoadPicturesAsync();  // 异步加载图片
            await LoadPostAsync();

            try
            {
                await getNotify.Get();  // 异步等待 getNotify.Get() 完成
                Notify_Grid.Visibility = Visibility.Visible;
            }
            catch
            {
                loadRing.Visibility = Visibility.Collapsed;
                loadErr.Visibility = Visibility.Visible;
            }
        }


        private async Task LoadBackgroundAsync()
        {
            string apiUrl = "https://prod-cn-alicdn-gamestarter.kurogame.com/pcstarter/prod/starter/10003_Y8xXrXk65DqFHEDgApn3cpK5lfczpFx5/G152/index.json";
            JObject response = await FetchData(apiUrl);

            string baseDownloadUrl = "https://pcdownload-wangsu.aki-game.com/";
            string zipUrl = baseDownloadUrl + response["animateBackground"]["url"].ToString();
            string newMd5 = response["animateBackground"]["md5"].ToString();

            string targetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"JSG-LLC\WaveTools\Background\");
            Directory.CreateDirectory(targetPath);
            string zipFilePath = Path.Combine(targetPath, "background.zip");

            string backgroundPath = Path.Combine(targetPath, "home_1.jpg");
            string iconPath = Path.Combine(targetPath, "slogan.png");
            string md5FilePath = Path.Combine(targetPath, "md5.txt");

            // 检查文件是否存在以及MD5是否匹配
            if (File.Exists(backgroundPath) && File.Exists(iconPath) && File.Exists(md5FilePath))
            {
                string cachedMd5 = await File.ReadAllTextAsync(md5FilePath);
                if (cachedMd5 == newMd5)
                {
                    // MD5匹配，直接加载
                    await LoadAdvertisementDataAsync(backgroundPath, iconPath);
                    return;
                }
            }

            // MD5不匹配或文件不存在，下载并解压
            await DownloadFileAsync(zipUrl, zipFilePath);
            ExtractZipFile(zipFilePath, targetPath);

            // 保存新的MD5
            await File.WriteAllTextAsync(md5FilePath, newMd5);

            await LoadAdvertisementDataAsync(backgroundPath, iconPath);
        }

        private async Task LoadPicturesAsync()
        {
            string apiUrl = "https://pcdownload-wangsu.aki-game.com/pcstarter/prod/starter/10003_Y8xXrXk65DqFHEDgApn3cpK5lfczpFx5/G152/guidance/zh-Hans.json";
            var response = await FetchPopulateData(apiUrl);
            var guidanceData = JsonConvert.DeserializeObject<GuidanceRoot>(response);

            // 填充图片数据
            PopulatePicturesAsync(guidanceData.slideshow);
        }

        private async Task LoadPostAsync()
        {
            await getNotify.Get();
            NotifyLoad.Visibility = Visibility.Collapsed;
            NotifyNav.Visibility = Visibility.Visible;
            // 查找第一个已启用的MenuItem并将其选中
            foreach (var menuItem in NotifyNav.Items)
            {
                if (menuItem is SelectorBarItem item && item.IsEnabled)
                {
                    NotifyNav.SelectedItem = item;
                    break;
                }
            }
        }

        private async Task LoadAdvertisementDataAsync(string backgroundPath, string iconPath)
        {
            Logging.Write("LoadAdvertisementData...", 0);
            Logging.Write("Getting Background: " + backgroundPath, 0);
            BitmapImage backgroundImage = new BitmapImage(new Uri(backgroundPath));
            BackgroundImage.Source = backgroundImage;

            // 设置按钮图标
            try
            {
                Logging.Write("Getting Button Image: " + iconPath, 0);
                BitmapImage iconImage = new BitmapImage(new Uri(iconPath));
                IconImageBrush.ImageSource = iconImage;
            }
            catch (Exception e)
            {
                Logging.Write("Getting Button Image Error: " + e.Message, 0);
            }

            var result = await GetUpdate.GetDependUpdate();
            var status = result.Status;
            if (status == 1)
            {
                NotificationManager.RaiseNotification("更新提示", "依赖包需要更新\n请尽快到[设置-检查依赖更新]进行更新", InfoBarSeverity.Warning);
            }
            result = await GetUpdate.GetWaveToolsUpdate();
            status = result.Status;
            if (status == 1)
            {
                NotificationManager.RaiseNotification("更新提示", "WaveTools有更新\n可到[设置-检查更新]进行更新", InfoBarSeverity.Warning);
            }

            loadRing.Visibility = Visibility.Collapsed;
        }

        private void BackgroundImage_ImageOpened(object sender, RoutedEventArgs e)
        {
            StartFadeAnimation(BackgroundImage, 0, 1, TimeSpan.FromSeconds(0.2));
            StartFadeAnimation(OpenUrlButton, 0, 1, TimeSpan.FromSeconds(0.2));
        }

        private void StartFadeAnimation(FrameworkElement target, double from, double to, TimeSpan duration)
        {
            DoubleAnimation opacityAnimation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = duration,
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(opacityAnimation, target);
            Storyboard.SetTargetProperty(opacityAnimation, "Opacity");

            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(opacityAnimation);
            storyboard.Begin();
        }

        private async Task<JObject> FetchData(string apiUrl)
        {
            Logging.Write("FetchData:" + apiUrl, 0);
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
                HttpResponseMessage response = await client.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();

                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    using (var decompressedStream = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress))
                    {
                        using (var reader = new StreamReader(decompressedStream))
                        {
                            string responseBody = await reader.ReadToEndAsync();
                            return JObject.Parse(responseBody);
                        }
                    }
                }
            }
        }

        private async Task<string> FetchPopulateData(string apiUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                return await client.GetStringAsync(apiUrl);
            }
        }


        public void PopulatePicturesAsync(List<Slideshow> slideshows)
        {
            foreach (var slideshow in slideshows)
            {
                Pictures.Add(slideshow.url);  // 将 URL 添加到集合
                list.Add(slideshow.jumpUrl);  // 将跳转 URL 添加到集合
            }
            FlipViewPipsPager.NumberOfPages = slideshows.Count;  // 一次性设置总页数
            Gallery_Grid.Visibility = Visibility.Visible;  // 显示 FlipView
        }



        private void Gallery_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // 获取当前选中的图片
            int selectedPicture = Gallery.SelectedIndex;

            // 如果选中了图片，则打开浏览器并导航到指定的网页
            string url = list[selectedPicture]; // 替换为要打开的网页地址
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }

        private void Notify_NavView_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            SelectorBarItem selectedItem = sender.SelectedItem;
            int currentSelectedIndex = sender.Items.IndexOf(selectedItem);
            switch (currentSelectedIndex)
            {
                case 0:
                    NotifyFrame.Navigate(typeof(NotifyNotificationView));
                    break;
                case 1:
                    NotifyFrame.Navigate(typeof(NotifyMessageView));
                    break;
                case 2:
                    NotifyFrame.Navigate(typeof(NotifyAnnounceView));
                    break;
            }
        }

        private async Task<BitmapImage> LoadImageAsync(string imageUrl)
        {
            // 检查缓存中是否已存在图片
            if (imageCache.ContainsKey(imageUrl))
            {
                return imageCache[imageUrl];
            }

            // 从网络加载图片
            BitmapImage bitmapImage = new BitmapImage();
            using (var stream = await new HttpClient().GetStreamAsync(imageUrl))
            using (var memStream = new MemoryStream())
            {
                await stream.CopyToAsync(memStream);
                memStream.Position = 0;
                var randomAccessStream = memStream.AsRandomAccessStream();
                await bitmapImage.SetSourceAsync(randomAccessStream);
            }

            // 将加载的图片添加到缓存中
            imageCache[imageUrl] = bitmapImage;

            return bitmapImage;
        }

        private async Task DownloadFileAsync(string url, string destinationPath)
        {
            using (HttpClient client = new HttpClient())
            {
                using (HttpResponseMessage response = await client.GetAsync(url))
                {
                    response.EnsureSuccessStatusCode();
                    using (FileStream fs = new FileStream(destinationPath, FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }
            }
        }

        private void ExtractZipFile(string zipFilePath, string extractPath)
        {
            using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destinationPath = Path.Combine(extractPath, entry.FullName);
                    entry.ExtractToFile(destinationPath, true);
                }
            }
        }

        public class GuidanceRoot
        {
            public Activity guidance { get; set; }
            public string desc { get; set; }
            public List<Slideshow> slideshow { get; set; }
        }

        public class Slideshow
        {
            public string jumpUrl { get; set; }
            public string md5 { get; set; }
            public string url { get; set; }
        }
    }

}