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
using WaveTools.Views.ToolViews;
using WaveTools.Views;
using System;

namespace WaveTools.Depend
{
    public class MainFrameController
    {
        private Frame mainFrame;

        public MainFrameController(Frame frame)
        {
            this.mainFrame = frame;
        }

        public void Navigate(string tag)
        {
            switch (tag)
            {
                case "home":
                    mainFrame.Navigate(typeof(MainView));
                    break;
                case "startgame":
                    mainFrame.Navigate(typeof(StartGameView));
                    break;
                case "gacha":
                    mainFrame.Navigate(typeof(GachaView));
                    break;
                case "donation":
                    mainFrame.Navigate(typeof(DonationView));
                    break;
                case "settings":
                    mainFrame.Navigate(typeof(AboutView));
                    break;
                default:
                    throw new ArgumentException("Unknown navigation tag", nameof(tag));
            }
        }
    }
}