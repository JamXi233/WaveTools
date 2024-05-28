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

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using WaveTools.Depend;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static WaveTools.App;
using System.Diagnostics;
using SRTools.Depend;

namespace WaveTools.Views.SGViews
{
    public sealed partial class AccountView : Page
    {

        public AccountView()
        {
            this.InitializeComponent();
            Logging.Write("Switch to AccountView", 0);
            InitData();
        }

        private async void InitData()
        {
            await LoadData(await ProcessRun.WaveToolsHelperAsync("/GetAllUser"));
            Account_Load.Visibility = Visibility.Collapsed;
        }

        private async Task LoadData(string jsonData)
        {
            StartGameView.SelectedUID = null;
            StartGameView.SelectedName = null;
            AccountListView.SelectionChanged -= AccountListView_SelectionChanged;
            loginNewAccount.IsEnabled = true;

            // 使用Task.Run在后台线程中执行反序列化操作
            List<Account> accounts = await Task.Run(() => JsonConvert.DeserializeObject<List<Account>>(jsonData));

            AccountListView.ItemsSource = accounts; // 确保将反序列化后的数据绑定到UI

            AccountListView.SelectionChanged += AccountListView_SelectionChanged;
            refreshAccount.Visibility = Visibility.Visible;
            refreshAccount_Loading.Visibility = Visibility.Collapsed;
        }

        private async void LoginAccount(object sender, RoutedEventArgs e)
        {
            loginAccount.IsOpen = true;
        }

        private async void LoginAccount_C(TeachingTip sender, object args)
        {
            loginAccount.IsOpen = false;
            GameStartUtil gameStartUtil = new GameStartUtil();
            gameStartUtil.StartGame();
            try
            {
                // 删除所有登录信息
                await ProcessRun.WaveToolsHelperAsync("/RemoveAllLogin");
                // 显示等待提示，并启用按钮以强行停止进程
                WaitOverlayManager.RaiseWaitOverlay(true, true, 0, "等待登录账号", "游戏已经启动，请登录您的账号记录下UID后退出游戏。", true, "取消登录", ProcessRun.StopWaveToolsHelperProcess);
                // 等待用户登录
                await ProcessRun.WaveToolsHelperAsync("/WaitForLogin");
            }
            finally
            {
                WaitOverlayManager.RaiseWaitOverlay(false);
                string isLogined = await ProcessRun.WaveToolsHelperAsync("/CheckUser");
                if (isLogined == "用户缓存文件存在")
                {
                    TextBox uidTextBox = new TextBox
                    {
                        PlaceholderText = "请输入UID"
                    };

                    TextBox nameTextBox = new TextBox
                    {
                        PlaceholderText = "请输入昵称"
                    };

                    StackPanel dialogStackPanel = new StackPanel();
                    dialogStackPanel.Children.Add(new TextBlock { Text = "UID:" });
                    dialogStackPanel.Children.Add(uidTextBox);
                    dialogStackPanel.Children.Add(new TextBlock { Text = "昵称:" });
                    dialogStackPanel.Children.Add(nameTextBox);

                    ContentDialog updateDialog = new ContentDialog
                    {
                        Title = "保存账号",
                        Content = dialogStackPanel,
                        CloseButtonText = "关闭",
                        PrimaryButtonText = "保存",
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = this.Content.XamlRoot // Assuming this is in a Page or UserControl
                    };

                    if (await updateDialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        string uid = uidTextBox.Text;
                        string name = nameTextBox.Text;

                        // 处理输入的UID和昵称
                        await ProcessRun.WaveToolsHelperAsync($"/SaveUser {uid} {name}");
                        RefreshAccount(null, null);
                    }
                }
            }
        }

        private void AccountListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AccountListView.SelectedItem != null)
            {
                renameAccount.IsEnabled = true;
                saveAccount.IsEnabled = true;
                deleteAccount.IsEnabled = true;
                Account selectedAccount = (Account)AccountListView.SelectedItem;
                StartGameView.SelectedUID = selectedAccount.uid;
                StartGameView.SelectedName = selectedAccount.name;
            }
        }

        private async void SaveAccount_Override(object sender, RoutedEventArgs e)
        {
            // 如果找到了别名，则直接使用当前 UID 和别名进行备份
            if (StartGameView.SelectedUID != null || StartGameView.SelectedName != null)
            {
                string backupResult = await ProcessRun.WaveToolsHelperAsync($"/SaveUser {StartGameView.SelectedUID} {StartGameView.SelectedName}");
                Console.WriteLine(backupResult); // 可以根据需要处理备份结果
            }
            else
            {
                Console.WriteLine("未找到当前 UID 和别名，无法进行覆盖保存。");
                // 这里可以添加逻辑，如显示错误信息或者采取其他操作
            }
        }

        public static async Task<string> GetAccountNameByUID(string UID)
        {
            // 从 WaveToolsHelper 获取所有备份账户的 JSON 数据
            string jsonData = await ProcessRun.WaveToolsHelperAsync("/GetAllUser");

            // 反序列化 JSON 数据为账户列表
            List<Account> accounts = JsonConvert.DeserializeObject<List<Account>>(jsonData);

            // 在账户列表中查找指定 UID 的账户
            Account account = accounts.Find(acc => acc.uid == UID);

            // 如果找到了对应的账户，则返回账户的名称；否则返回空字符串
            return account != null ? account.name : "";
        }

        private async void SaveAccount_C(object sender, RoutedEventArgs e)
        {
            saveAccountName.IsOpen = false;
            saveAccountSuccess.Subtitle = await ProcessRun.WaveToolsHelperAsync("/SaveUser " + saveAccountNameInput.Text);
            saveAccountSuccess.IsOpen = true;
            renameAccount.IsEnabled = true;
            saveAccount.IsEnabled = true;
            saveAccountNameInput.Text = "";
            RefreshAccount(null, null);
        }

        private async void RenameAccount_C(object sender, RoutedEventArgs e)
        {
            renameAccountTip.IsOpen = false;
            Account selectedAccount = (Account)AccountListView.SelectedItem;
            renameAccountSuccess.Subtitle = await ProcessRun.WaveToolsHelperAsync($"/RenameUser {selectedAccount.uid} {selectedAccount.name} {renameAccountNameInput.Text}");
            renameAccountSuccess.IsOpen = true;
            RefreshAccount(null, null);
        }

        private async void RemoveAccount_C(TeachingTip sender, object args)
        {
            removeAccountCheck.IsOpen = false;
            Account selectedAccount = (Account)AccountListView.SelectedItem;
            removeAccountSuccess.Subtitle = await ProcessRun.WaveToolsHelperAsync($"/RemoveUser {selectedAccount.uid} {selectedAccount.name}");
            removeAccountSuccess.IsOpen = true;
            RefreshAccount(null,null);
        }

        private void DeleteAccount(object sender, RoutedEventArgs e)
        {
            removeAccountCheck.IsOpen = true;
        }

        private void RenameAccount(object sender, RoutedEventArgs e)
        {
            renameAccountTip.IsOpen = true;
        }

        private async void RefreshAccount(object sender, RoutedEventArgs e)
        {
            saveAccount.IsEnabled = false;
            loginNewAccount.IsEnabled = false;
            deleteAccount.IsEnabled = false;
            renameAccount.IsEnabled = false;
            refreshAccount.Visibility = Visibility.Collapsed;
            refreshAccount_Loading.Visibility = Visibility.Visible;
            await LoadData(await ProcessRun.WaveToolsHelperAsync("/GetAllUser"));
        }


    }
    public class Account
    {
        public string uid { get; set; }
        public string name { get; set; }
    }
}