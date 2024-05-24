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
            await LoadData(await ProcessRun.WaveToolsHelperAsync("/GetAllBackup"));
            Account_Load.Visibility = Visibility.Collapsed;
        }

        private async Task LoadData(string jsonData)
        {
            refreshAccount.Visibility = Visibility.Collapsed;
            refreshAccount_Loading.Visibility = Visibility.Visible;
            var accountexist = false;
            AccountListView.SelectionChanged -= AccountListView_SelectionChanged;
            var CurrentLoginUID = await ProcessRun.WaveToolsHelperAsync("/GetCurrentLogin");
            List<Account> accounts = JsonConvert.DeserializeObject<List<Account>>(jsonData);

            AccountListView.ItemsSource = accounts;
            if (accounts != null)
            {
                foreach (Account account in accounts)
                {
                    if (account.uid == CurrentLoginUID)
                    {
                        accountexist = true;
                        AccountListView.SelectedItem = account;
                        break;
                    }
                }
            }
            if (!accountexist)
            {
                saveAccount.IsEnabled = false;
                renameAccount.IsEnabled = false;
                deleteAccount.IsEnabled = false;
            }

            if (!accountexist && CurrentLoginUID != "0" && CurrentLoginUID != "目前未登录ID")
            {
                // 创建一个新的 Account 对象，用于表示不存在于 accounts 列表中的 UID
                Account newAccount = new Account { uid = CurrentLoginUID, name = "未保存", nuser = true };

                // 将新的 Account 对象添加到 accounts 列表中
                accounts.Add(newAccount);

                // 将 accounts 列表设置为 AccountListView 的 ItemsSource
                AccountListView.ItemsSource = accounts;

                // 将新的 Account 对象作为 AccountListView 的 SelectedItem
                AccountListView.SelectedItem = newAccount;
                saveAccount.IsEnabled = true;
                renameAccount.IsEnabled = false;
            }
            AccountListView.SelectionChanged += AccountListView_SelectionChanged;
            refreshAccount.Visibility = Visibility.Visible;
            refreshAccount_Loading.Visibility = Visibility.Collapsed;
        }


        private async void AccountListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AccountListView.SelectedItem != null)
            {
                Account selectedAccount = (Account)AccountListView.SelectedItem;

                // 根据 nuser 属性执行相应的命令
                string command = selectedAccount.nuser
                                 ? $"/RestoreNUser {selectedAccount.uid} {selectedAccount.name}"
                                 : $"/RestoreUser {selectedAccount.uid} {selectedAccount.name}";

                await ProcessRun.WaveToolsHelperAsync(command);
                saveAccount.IsEnabled = true;
                renameAccount.IsEnabled = true;
                deleteAccount.IsEnabled = true;
            }
            await LoadData(await ProcessRun.WaveToolsHelperAsync("/GetAllBackup"));
        }




        private async void SaveAccount(object sender, RoutedEventArgs e)
        {
            var CurrentLoginUID = await ProcessRun.WaveToolsHelperAsync("/GetCurrentLogin");
            List<Account> accounts = JsonConvert.DeserializeObject<List<Account>>(await ProcessRun.WaveToolsHelperAsync("/GetAllBackup"));
            bool found = false;
            if (accounts != null)
            {
                foreach (Account account in accounts)
                {
                    if (account.uid == CurrentLoginUID)
                    {
                        found = true;
                        break;
                    }
                }
                if (found)
                {
                    saveAccountFound.IsOpen = true;
                    saveAccountFound.Subtitle = "是否要覆盖保存UID:" + CurrentLoginUID;
                }
                else
                {
                    saveAccountUID.Text = "将要保存的UID为:"+ CurrentLoginUID;
                    saveAccountName.IsOpen = true;
                }
            }
            else
            {
                saveAccountUID.Text = "将要保存的UID为:"+ CurrentLoginUID;
                saveAccountName.IsOpen = true;
            }
            
        }

        private async void SaveAccount_Override(TeachingTip sender, object args)
        {
            var CurrentLoginUID = await ProcessRun.WaveToolsHelperAsync("/GetCurrentLogin");
            // 从 WaveToolsHelper 获取当前 UID 的别名
            string currentName = await GetAccountNameByUID(CurrentLoginUID);

            // 如果找到了别名，则直接使用当前 UID 和别名进行备份
            if (!string.IsNullOrEmpty(currentName))
            {
                string backupResult = await ProcessRun.WaveToolsHelperAsync($"/BackupNUser {currentName}");
                Console.WriteLine(backupResult); // 可以根据需要处理备份结果
            }
            else
            {
                Console.WriteLine("未找到当前 UID 的别名，无法进行覆盖保存。");
                // 这里可以添加逻辑，如显示错误信息或者采取其他操作
            }
        }

        public static async Task<string> GetAccountNameByUID(string UID)
        {
            // 从 WaveToolsHelper 获取所有备份账户的 JSON 数据
            string jsonData = await ProcessRun.WaveToolsHelperAsync("/GetAllBackup");

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
            saveAccountSuccess.Subtitle = await ProcessRun.WaveToolsHelperAsync("/BackupNUser " + saveAccountNameInput.Text);
            saveAccountSuccess.IsOpen = true;
            renameAccount.IsEnabled = true;
            saveAccount.IsEnabled = true;
            saveAccountNameInput.Text = "";
            await LoadData(await ProcessRun.WaveToolsHelperAsync("/GetAllBackup"));
        }

        private async void RenameAccount_C(object sender, RoutedEventArgs e)
        {
            renameAccountTip.IsOpen = false;
            Account selectedAccount = (Account)AccountListView.SelectedItem;
            renameAccountSuccess.Subtitle = await ProcessRun.WaveToolsHelperAsync($"/RenameNUser {selectedAccount.uid} {selectedAccount.name} {renameAccountNameInput.Text}");
            renameAccountSuccess.IsOpen = true;
            await LoadData(await ProcessRun.WaveToolsHelperAsync("/GetAllBackup"));
        }

        private async void RemoveAccount_C(TeachingTip sender, object args)
        {
            removeAccountCheck.IsOpen = false;
            Account selectedAccount = (Account)AccountListView.SelectedItem;
            string NUser = selectedAccount.DisplayName.Contains("旧版本")? "0":"1";
            removeAccountSuccess.Subtitle = await ProcessRun.WaveToolsHelperAsync($"/RemoveUser {selectedAccount.uid} {selectedAccount.name} {NUser}");
            removeAccountSuccess.IsOpen = true;
            await LoadData(await ProcessRun.WaveToolsHelperAsync("/GetAllBackup"));
            /*List<Account> accounts = JsonConvert.DeserializeObject<List<Account>>(await ProcessRun.WaveToolsHelperAsync("/GetAllBackup"));
            AccountListView.ItemsSource = accounts;
            if (accounts is not null)
            {
                AccountListView.SelectedIndex = 0;
            }*/
        }

        private async void GetCurrentAccount(object sender, RoutedEventArgs e)
        {
            currentAccountTip.IsOpen = true;
            currentAccountTip.Subtitle = "当前UID为:"+ await ProcessRun.WaveToolsHelperAsync("/GetCurrentLogin");
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
            await LoadData(await ProcessRun.WaveToolsHelperAsync("/GetAllBackup"));
        }


    }
    public class Account
    {
        public string uid { get; set; }
        public string name { get; set; }
        public bool nuser { get; set; }

        public string DisplayName
        {
            get
            {
                return nuser ? name : name + " (旧版本)";
            }
        }
    }



}