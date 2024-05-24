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

namespace WaveTools.Views.FirstRunViews
{
    public sealed partial class FirstRunAnimation : Page
    {
        public static bool isOldDataExist;
        public FirstRunAnimation()
        {
            this.InitializeComponent();
            Logging.Write("Switch to FirstRunAnimation", 0);
            StartMergeData();
        }

        private void StartMergeData()
        {
            Frame parentFrame = GetParentFrame(this);
            AppDataController appDataController = new AppDataController();
            if (appDataController.CheckOldData() == 1)
            {
                FirstRunAnimation_Status.Text = "正在合并旧版本配置文件...";
                AppDataController.SetFirstRun(0);
                FirstRunAnimation_Status.Text = "合并完成";
            }
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