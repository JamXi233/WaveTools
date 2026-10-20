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
using System.Threading.Tasks;
using WaveTools.Depend;
using System.Diagnostics;
using Microsoft.UI.Xaml.Media;

namespace WaveTools.Views.FirstRunViews
{
    public sealed partial class FirstRunTheme : Page
    {
        public FirstRunTheme()
        {
            this.InitializeComponent();
            Logging.Write("Switch to FirstRunTheme", 0);
            AppDataController.SetFirstRunStatus(3);
            Loaded += FirstRunTheme_Loaded;
            int dayNight = AppDataController.GetDayNight();
            if (dayNight == 0)
            {
                FollowSystemButton.IsChecked = true;
            }
            if (dayNight == 1)
            {
                DayModeButton.IsChecked = true;
            }
            if (dayNight == 2)
            {
                NightModeButton.IsChecked = true;
            }
        }

        private void FirstRunTheme_Loaded(object sender, RoutedEventArgs e)
        {
            if (AppDataController.HasThemeSetting())
            {
                Frame parentFrame = GetParentFrame(this);
                FirstRunNavigation.NavigateToNextRequiredPage(parentFrame);
            }
        }

        private void FollowSystemButton_Click(object sender, RoutedEventArgs e)
        {
            SetTheme(ThemeMode.System);
            ChangeThemeRestartApp();
        }

        private void DayModeButton_Click(object sender, RoutedEventArgs e)
        {
            SetTheme(ThemeMode.Light);
            ChangeThemeRestartApp();
        }

        private void NightModeButton_Click(object sender, RoutedEventArgs e)
        {
            SetTheme(ThemeMode.Dark);
            ChangeThemeRestartApp();
        }

        private void ThemeFinish_Click(object sender, RoutedEventArgs e)
        {
            SaveSelectedTheme();

            Frame parentFrame = GetParentFrame(this);
            FirstRunNavigation.NavigateToNextRequiredPage(parentFrame);
        }

        private Frame GetParentFrame(FrameworkElement child)
        {

            DependencyObject parent = VisualTreeHelper.GetParent(child);

            while (parent != null && !(parent is Frame))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            return parent as Frame;
        }

        private void SetTheme(ThemeMode mode)
        {
            AppDataController.SetDayNight((int)mode);
        }

        private void SaveSelectedTheme()
        {
            if (FollowSystemButton.IsChecked == true)
            {
                SetTheme(ThemeMode.System);
                return;
            }

            if (DayModeButton.IsChecked == true)
            {
                SetTheme(ThemeMode.Light);
                return;
            }

            SetTheme(ThemeMode.Dark);
        }

        private async void ChangeThemeRestartApp()
        {
            // 获取当前应用程序的进程 ID 和文件路径
            var processId = Process.GetCurrentProcess().Id;
            var fileName = Process.GetCurrentProcess().MainModule.FileName;

            // 启动一个新的应用程序实例
            ProcessStartInfo info = new ProcessStartInfo(fileName)
            {
                UseShellExecute = true,
            };
            Process.Start(info);

            // 给新的实例一些时间来启动
            await Task.Delay(100); // 延迟时间可能需要根据应用程序的启动时间来调整

            // 关闭当前应用程序实例
            Process.GetProcessById(processId).Kill();
        }
    }

    public enum ThemeMode
    {
        System = 0,
        Light = 1,
        Dark = 2
    }

}