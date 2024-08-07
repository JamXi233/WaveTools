﻿// Copyright (c) 2021-2024, JamXi JSG-LLC.
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
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WaveTools.Views.SGViews
{
    public sealed partial class GameSettingsView : Page
    {

        public GameSettingsView()
        {
            this.InitializeComponent();
            Logging.Write("Switch to GameSettingsView", 0);
            InitData();
        }

        private async void InitData()
        {
            
        }

    }

}