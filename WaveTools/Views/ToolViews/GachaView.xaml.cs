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
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;
using WaveTools.Depend;
using System.Linq;
using Windows.Storage;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Collections.Generic;
using WaveTools.Views.GachaViews;
using Spectre.Console;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using Microsoft.UI.Xaml.Media;
using static WaveTools.App;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml.Data;
using System.Threading;


namespace WaveTools.Views.ToolViews
{
public class GachaViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    public event Action RequestViewUpdate;

    private ObservableCollection<string> _uidList;
    public ObservableCollection<string> UidList
    {
        get => _uidList;
        set
        {
            _uidList = value;
            OnPropertyChanged(nameof(UidList));
        }
    }

    public GachaViewModel()
    {
        UidList = new ObservableCollection<string>();
        LoadUidsAsync();
    }

    private string _selectedUid;
    public string SelectedUid
    {
        get => _selectedUid;
        set
        {
            if (_selectedUid != value)
            {
                _selectedUid = value;
                OnPropertyChanged(nameof(SelectedUid));
                Logging.Write($"选择UID: {_selectedUid}");
            }
        }
    }

    public async Task LoadUidsAsync()
    {
        var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        dispatcherQueue.TryEnqueue(async () => {
            UidList.Clear();
            string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JSG-LLC", "WaveTools", "GachaRecords");
            if (Directory.Exists(baseDir))
            {
                var directories = await Task.Run(() => Directory.GetDirectories(baseDir));
                foreach (var dir in directories)
                {
                    string uid = Path.GetFileName(dir); // 这应该只获取目录的最后一部分，即 UID
                    UidList.Add(uid);
                }

                SelectedUid = UidList.Any() ? UidList[0] : null;
            }
        });
    }

    public void ReloadData()
    {
        _ = LoadUidsAsync();  // 异步加载UIDs，无需等待完成
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}


    public sealed partial class GachaView : Page
    {

        public bool isProxyRunning;
        public static String selectedUid;
        public String GachaLink_String;
        public String GachaLinkCache_String;
        public bool isClearGachaSaved;

        private DispatcherQueueTimer dispatcherTimer;
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        public GachaView()
        {
            InitializeComponent();
            Logging.Write("Switch to GachaView", 0);
            var viewModel = new GachaViewModel();
            DataContext = viewModel;
            InitData();
            viewModel.RequestViewUpdate += ViewModel_RequestViewUpdate;
        }

        private async void ViewModel_RequestViewUpdate()
        {
            // 确保 selectedUid 已正确设置
            if (!string.IsNullOrEmpty(selectedUid))
            {
                await LoadData(selectedUid);
            }
        }

        private async void InitData()
        {
            InitTimer();

            // 指定文件夹路径
            string directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JSG-LLC", "WaveTools", "GachaRecords");

            // 检查目录是否存在，如果不存在，则创建它
            if (Directory.Exists(directoryPath))
            {
                // 获取指定路径下的所有子目录
                string[] subDirectories = Directory.GetDirectories(directoryPath);

                // 检查是否存在任何子目录
                if (subDirectories.Length > 0)
                {
                    await UpdateAndDeleteGachaRecordsAsync();
                }
                else
                {
                    await UpdateAndDeleteGachaRecordsAsync();
                    GachaViewModel viewModel = this.DataContext as GachaViewModel;
                    if (viewModel != null)
                    {
                        await viewModel.LoadUidsAsync();  // 加载数据
                        GachaRecordsUID.DataContext = viewModel;  // 确保DataContext正确
                        GachaRecordsUID.SetBinding(ComboBox.SelectedItemProperty, new Binding
                        {
                            Path = new PropertyPath("SelectedUid"),
                            Mode = BindingMode.TwoWay
                        });
                    }
                    if (subDirectories.Length <= 0)
                    {
                        gachaView.Visibility = Visibility.Collapsed;
                        loadGachaProgress.Visibility = Visibility.Visible;
                        loadGachaFailedIcon.Visibility = Visibility.Visible;
                        loadGachaProgressRing.Visibility = Visibility.Collapsed;
                        loadGachaText.Text = "无抽卡记录";
                        ClearGacha.IsEnabled = false;
                        ExportSRGF.IsEnabled = false;
                        ImportSRGF.IsEnabled = true;
                        localSettings.Values["Gacha_Data"] = "0";
                        Logging.Write("无抽卡记录");
                    }
                    else
                    {
                        await UpdateAndDeleteGachaRecordsAsync();
                    }
                }
            }
            else
            {
                Directory.CreateDirectory(directoryPath); // CreateDirectory 不会重复创建已存在的目录
                InitData();
            }
        }

        private void InitTimer() 
        {
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            dispatcherTimer = dispatcherQueue.CreateTimer();
            dispatcherTimer.Interval = TimeSpan.FromSeconds(2);
            dispatcherTimer.Tick += CheckProcess;
        }

        private async void GachaRecordsUID_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GachaRecordsUID.SelectedItem != null)
            {
                selectedUid = GachaRecordsUID.SelectedItem as string;
                await LoadData(selectedUid);
            }
        }

        public static string GetSelectedUid() 
        {
            return selectedUid;
        }

        private async Task UpdateAndDeleteGachaRecordsAsync()
        {
            string uDFP = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string[] fileNames = { "GachaRecords_Character.ini", "GachaRecords_LightCone.ini",
                           "GachaRecords_Newbie.ini", "GachaRecords_Regular.ini" };

            List<FileInfo> files = fileNames.Select(name => new FileInfo(Path.Combine(uDFP, "JSG-LLC", "WaveTools", name))).ToList();

            if (files.Any(file => file.Exists))
            {
                await GachaRecords.UpdateGachaRecordsAsync();

                // Attempt to delete files
                foreach (var file in files)
                {
                    try
                    {
                        if (file.Exists)
                        {
                            file.Delete();
                            Console.WriteLine($"已删除旧版本抽卡记录: {file.FullName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"无法删除{file.FullName}: {ex.Message}");
                    }
                }
            }
            else
            {
                Logging.Write("无旧版本抽卡记录更新/删除");
            }
        }


        public async Task LoadData(string UID)
        {
            ReloadGachaFrame();
            gachaView.Visibility = Visibility.Collapsed;
            gachaFrame.Visibility = Visibility.Collapsed;
            loadGachaProgress.Visibility = Visibility.Visible;
            loadGachaFailedIcon.Visibility = Visibility.Collapsed;
            loadGachaProgressRing.Visibility = Visibility.Visible;
            loadGachaText.Text = "等待刷新列表";
            CharacterGachaSelect.IsEnabled = false;
            LightConeGachaSelect.IsEnabled = false;
            NewbieGachaSelect.IsEnabled = false;
            RegularGachaSelect.IsEnabled = false;
            string uDFP = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            string FileFolder = "\\JSG-LLC\\WaveTools\\GachaRecords\\" + UID + "\\GachaRecords_Character.ini";
            string FileFolder2 = "\\JSG-LLC\\WaveTools\\GachaRecords\\" + UID + "\\GachaRecords_LightCone.ini";
            string FileFolder3 = "\\JSG-LLC\\WaveTools\\GachaRecords\\" + UID + "\\GachaRecords_Newbie.ini";
            string FileFolder4 = "\\JSG-LLC\\WaveTools\\GachaRecords\\" + UID + "\\GachaRecords_Regular.ini";

            if (File.Exists(uDFP + FileFolder) || File.Exists(uDFP + FileFolder2) || File.Exists(uDFP + FileFolder3) || File.Exists(uDFP + FileFolder4))
            {
                ClearGacha.IsEnabled = true;
                ExportSRGF.IsEnabled = true;
                ImportSRGF.IsEnabled = true;
                gachaView.Visibility = Visibility.Visible;
                gachaFrame.Visibility = Visibility.Visible;
                loadGachaProgress.Visibility = Visibility.Collapsed;
                localSettings.Values["Gacha_Data"] = "1";
                if (File.Exists(uDFP + FileFolder)) CharacterGachaSelect.IsEnabled = true;
                if (File.Exists(uDFP + FileFolder2)) LightConeGachaSelect.IsEnabled = true;
                if (File.Exists(uDFP + FileFolder3)) NewbieGachaSelect.IsEnabled = true;
                if (File.Exists(uDFP + FileFolder4)) RegularGachaSelect.IsEnabled = true;
                ReloadGachaFrame();
            }
            else
            {
                gachaView.Visibility = Visibility.Collapsed;
                loadGachaProgress.Visibility = Visibility.Visible;
                loadGachaFailedIcon.Visibility = Visibility.Visible;
                loadGachaProgressRing.Visibility = Visibility.Collapsed;
                loadGachaText.Text = "无抽卡记录";
                ClearGacha.IsEnabled = false;
                ExportSRGF.IsEnabled = false;
                ImportSRGF.IsEnabled = true;
                localSettings.Values["Gacha_Data"] = "0";
                Logging.Write("无抽卡记录");
            }
        }


        private async void ProxyButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void CheckProcess(DispatcherQueueTimer timer, object e)
        {
            
        }

        private async void LoadDataAsync(String url)
        {
            ProxyButton.IsEnabled = false;
            ExportSRGF.IsEnabled = false;
            ImportSRGF.IsEnabled = false;

            gacha_status.Text = "正在获取角色池";
            var char_records = await new GachaRecords().GetAllGachaRecordsAsync(url, null,"11");
            gacha_status.Text = "正在获取光锥池";
            var light_records = await new GachaRecords().GetAllGachaRecordsAsync(url, null,"12");
            gacha_status.Text = "正在获取新手池";
            var newbie_records = await new GachaRecords().GetAllGachaRecordsAsync(url, null, "2");
            gacha_status.Text = "正在获取常驻池";
            var regular_records = await new GachaRecords().GetAllGachaRecordsAsync(url, null, "1");
            if (char_records != null && char_records.Count > 0)
            {
                if (char_records[0].Uid.Length != 9)
                {
                    Logging.Write("抽卡链接UID=" + char_records[0].Uid, 1);
                    gacha_status.Text = "获取API信息出现问题，可能是抽卡链接已过期，请重新获取";
                    GachaLink.IsOpen = false;
                }
                else 
                {
                    var folder = KnownFolders.DocumentsLibrary;
                    var WaveToolsFolder = await folder.CreateFolderAsync("JSG-LLC\\WaveTools", CreationCollisionOption.OpenIfExists);
                    var char_gachaFile = await WaveToolsFolder.CreateFileAsync("GachaRecords_Character.ini", CreationCollisionOption.OpenIfExists);
                    var light_gachaFile = await WaveToolsFolder.CreateFileAsync("GachaRecords_LightCone.ini", CreationCollisionOption.OpenIfExists);
                    var newbie_gachaFile = await WaveToolsFolder.CreateFileAsync("GachaRecords_Newbie.ini", CreationCollisionOption.OpenIfExists);
                    var regular_gachaFile = await WaveToolsFolder.CreateFileAsync("GachaRecords_Regular.ini", CreationCollisionOption.OpenIfExists);
                    var char_serializedList = JsonConvert.SerializeObject(char_records);//获取到的数据
                    var light_serializedList = JsonConvert.SerializeObject(light_records);//获取到的数据
                    var newbie_serializedList = JsonConvert.SerializeObject(newbie_records);//获取到的数据
                    var regular_serializedList = JsonConvert.SerializeObject(regular_records);//获取到的数据
                    Logging.Write("正在获取API信息,请不要退出... | 正在获取角色池...", 0);
                    DataChange(char_serializedList, char_gachaFile);
                    Logging.Write("正在获取API信息,请不要退出... | 正在获取光锥池...", 0);
                    DataChange(light_serializedList, light_gachaFile);
                    Logging.Write("正在获取API信息,请不要退出... | 正在获取新手池...", 0);
                    DataChange(newbie_serializedList, newbie_gachaFile);
                    Logging.Write("正在获取API信息,请不要退出... | 正在获取常驻池...", 0);
                    DataChange(regular_serializedList, regular_gachaFile);
                }
            }
            ProxyButton.IsEnabled = true;
            ExportSRGF.IsEnabled = true;
        }

        private async void DataChange(String serializedList, StorageFile gachaFile) 
        {
            
            var GachaRecords = FileIO.ReadTextAsync(gachaFile).AsTask().GetAwaiter().GetResult();//原来的数据

            if (GachaRecords != "") //如果不为空
            {
                // 反序列化为List<Record>对象
                List<GachaRecords> data1 = JsonConvert.DeserializeObject<List<GachaRecords>>(serializedList);
                List<GachaRecords> data2 = JsonConvert.DeserializeObject<List<GachaRecords>>(GachaRecords);
                // 合并数据，确保ID不重复
                var combinedData = data1.Concat(data2)
                            .GroupBy(r => r.Id)
                            .Select(g => g.First())
                            .ToList();
                string combinedDataJson = JsonConvert.SerializeObject(combinedData);
                // 如果需要，将合并后的数据序列化为JSON字符串
                // 消除ID为空的记录
                JArray data = JArray.Parse(combinedDataJson);
                for (int i = data.Count - 1; i >= 0; i--)
                {
                    if (data[i]["Id"].ToString() == "")
                    {
                        data.RemoveAt(i);
                    }
                }
                combinedDataJson = JsonConvert.SerializeObject(data);
                await FileIO.WriteTextAsync(gachaFile, combinedDataJson);
            }
            else
            {
                await FileIO.WriteTextAsync(gachaFile, serializedList);
            }
            GachaLink.IsOpen = false;
            gacha_status.Text = "API获取完成";
            await Depend.GachaRecords.UpdateGachaRecordsAsync();

            WaitOverlayManager.RaiseWaitOverlay(false);
            ProxyButton.IsEnabled = true;
        }

        private void ExportSRGF_Click(object sender, RoutedEventArgs e)
        {
            ExportSRGF exportSRGF = new ExportSRGF();
            _ = exportSRGF.ExportAll();
        }

        private async void ImportSRGF_Click(object sender, RoutedEventArgs e)
        {
            ImportSRGF importSRGF = new ImportSRGF();
            await importSRGF.Main();
            await UpdateAndDeleteGachaRecordsAsync();
            GachaViewModel viewModel = this.DataContext as GachaViewModel;
            if (viewModel != null)
            {
                await viewModel.LoadUidsAsync();  // 加载数据
                GachaRecordsUID.DataContext = viewModel;  // 确保DataContext正确
                GachaRecordsUID.SetBinding(ComboBox.SelectedItemProperty, new Binding
                {
                    Path = new PropertyPath("SelectedUid"),
                    Mode = BindingMode.TwoWay
                });
            }
        }

        private void NavView_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        {
            SelectorBarItem selectedItem = sender.SelectedItem;
            int currentSelectedIndex = sender.Items.IndexOf(selectedItem);

            switch (currentSelectedIndex)
            {
                case 0:
                    gachaFrame.Navigate(typeof(CharacterGachaView));
                    break;
                case 1:
                    gachaFrame.Navigate(typeof(LightConeGachaView));
                    break;
                case 2:
                    gachaFrame.Navigate(typeof(RegularGachaView));
                    break;
                case 3:
                    gachaFrame.Navigate(typeof(NewbieGachaView));
                    break;
            }
        }

        private async void ClearGacha_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new ContentDialog();

            // XamlRoot must be set in the case of a ContentDialog running in a Desktop app
            dialog.XamlRoot = this.XamlRoot;
            dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Microsoft.UI.Xaml.Style;
            dialog.Title = "确定要清空您的抽卡记录吗？";
            dialog.Content = "确保您已经做好SRGF兼容格式或WaveTools的备份";
            dialog.PrimaryButtonText = "备份后删除";
            dialog.SecondaryButtonText = "直接删除";
            dialog.CloseButtonText = "取消";
            dialog.DefaultButton = ContentDialogButton.Primary;
            // 设置主按钮的点击事件处理程序
            dialog.PrimaryButtonClick += async (dialogSender, dialogArgs) =>
            {
                ExportSRGF exportSRGF = new ExportSRGF();
                if (await exportSRGF.ExportAll()) { ClearGacha_Run(); }
            };

            // 设置次要按钮的点击事件处理程序
            dialog.SecondaryButtonClick += (dialogSender, dialogArgs) =>
            {
                ClearGacha_Run();
            };

            var result = await dialog.ShowAsync();
        }

        private async void ClearGacha_Run()
        {
            // 构建文件夹路径
            string directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JSG-LLC", "WaveTools", "GachaRecords", selectedUid);

            // 删除文件夹及其所有内容
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, true); // true 参数允许递归删除
            }
            selectedUid = null;
            ReloadGachaFrame();
            // 更新ViewModel
            GachaViewModel viewModel = this.DataContext as GachaViewModel;
            if (viewModel != null)
            {
                await viewModel.LoadUidsAsync();  // 加载数据
                GachaRecordsUID.DataContext = viewModel;  // 确保DataContext正确
                GachaRecordsUID.SetBinding(ComboBox.SelectedItemProperty, new Binding
                {
                    Path = new PropertyPath("SelectedUid"),
                    Mode = BindingMode.TwoWay
                });
            }
            await LoadData(selectedUid);
            ReloadGachaFrame();
        }

        private void ReloadGachaFrame()
        {
            if (selectedUid != null)
            {
                gachaNav.Visibility = Visibility.Visible;
                gachaNav.SelectedItem = null;
                // 查找第一个已启用的MenuItem并将其选中
                foreach (var menuItem in gachaNav.Items)
                {
                    if (menuItem is SelectorBarItem item && item.IsEnabled)
                    {
                        gachaNav.SelectedItem = item;
                        break;
                    }
                }
            }
            else 
            {
                gachaNav.Visibility = Visibility.Collapsed;
            }
            
        }

        private void Disable_NavBtns()
        {
            NavigationView parentNavigationView = GetParentNavigationView(this);
            if (parentNavigationView != null)
            {
                var selectedItem = parentNavigationView.SelectedItem;
                var excludeTags = new HashSet<string> { "account_status", "event", "account" };  // 需要排除的标签

                foreach (var menuItem in parentNavigationView.MenuItems.Concat(parentNavigationView.FooterMenuItems))
                {
                    if (menuItem is NavigationViewItem navViewItem && navViewItem != selectedItem && !excludeTags.Contains(navViewItem.Tag as string))
                    {
                        navViewItem.IsEnabled = false;
                    }
                }
                // 特别处理设置按钮
                if (parentNavigationView.SettingsItem is NavigationViewItem settingsItem && settingsItem != selectedItem)
                {
                    settingsItem.IsEnabled = false;
                }
            }
        }

        private void Enable_NavBtns()
        {
            NavigationView parentNavigationView = GetParentNavigationView(this);
            if (parentNavigationView != null)
            {
                var selectedItem = parentNavigationView.SelectedItem;
                var excludeTags = new HashSet<string> { "account_status", "event", "account" };  // 需要排除的标签

                foreach (var menuItem in parentNavigationView.MenuItems.Concat(parentNavigationView.FooterMenuItems))
                {
                    if (menuItem is NavigationViewItem navViewItem && navViewItem != selectedItem && !excludeTags.Contains(navViewItem.Tag as string))
                    {
                        navViewItem.IsEnabled = true;
                    }
                }
                // 特别处理设置按钮
                if (parentNavigationView.SettingsItem is NavigationViewItem settingsItem && settingsItem != selectedItem)
                {
                    settingsItem.IsEnabled = true;
                }
            }
        }

        private void SetButtonsEnabledState(DependencyObject parent, bool isEnabled)
        {
            var childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Button button)
                {
                    button.IsEnabled = isEnabled;
                }
                else
                {
                    SetButtonsEnabledState(child, isEnabled);
                }
            }
        }



        private NavigationView GetParentNavigationView(FrameworkElement child)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);

            while (parent != null && !(parent is NavigationView))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            return parent as NavigationView;
        }


        private void Window_Closed(object sender, EventArgs e)
        {
            dispatcherTimer.Stop();
            dispatcherTimer.Tick -= CheckProcess;
        }

        
    }
}