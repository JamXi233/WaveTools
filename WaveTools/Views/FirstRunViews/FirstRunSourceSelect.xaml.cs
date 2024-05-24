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
using Microsoft.UI.Xaml.Media;
using WaveTools.Depend;
using Windows.Storage;

namespace WaveTools.Views.FirstRunViews
{
    public sealed partial class FirstRunSourceSelect : Page
    {
        public FirstRunSourceSelect()
        {
            this.InitializeComponent();
            Logging.Write("Switch to FirstRunSourceSelect", 0);
            AppDataController.SetFirstRunStatus(3);
        }

        //选择下载渠道开始
        private void DSerivceChooseFinish()
        {;
            Frame parentFrame = GetParentFrame(this);
            if (parentFrame != null)
            {
                // 前往下载依赖页面
                parentFrame.Navigate(typeof(FirstRunGetDepend));
            }
        }

        private void DService_Github_Choose(object sender, RoutedEventArgs e)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["Config_UpdateService"] = 0;
            DSerivceChooseFinish();
        }

        private void DService_Gitee_Choose(object sender, RoutedEventArgs e)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["Config_UpdateService"] = 1;
            DSerivceChooseFinish();
        }

        private void DService_JSG_Choose(object sender, RoutedEventArgs e)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["Config_UpdateService"] = 2;
            DSerivceChooseFinish();
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
    }
}