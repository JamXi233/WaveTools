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

using Microsoft.UI.Xaml.Controls;
using SRTools.Depend;
using System.Threading.Tasks;
using WaveTools.Depend;

namespace WaveTools.Views.FirstRunViews
{
    public sealed partial class FirstRunFinish : Page
    {
        public FirstRunFinish()
        {
            this.InitializeComponent();
            Logging.Write("Switch to FirstRunFinish", 0);
            Logging.Write("Thanks For Using WaveTools!", 0);
            _ = SetFirstRunCompletedAsync();

        }

        private async Task SetFirstRunCompletedAsync()
        {
            // 延迟两秒
            await Task.Delay(2000);

            // 两秒后执行的操作
            AppDataController.SetFirstRun(0);

        }

    }

}