using Microsoft.UI.Xaml.Controls;
using WaveTools.Depend;

namespace WaveTools.Views.FirstRunViews
{
    internal static class FirstRunNavigation
    {
        public static void NavigateToNextRequiredPage(Frame parentFrame)
        {
            if (parentFrame == null)
            {
                return;
            }

            if (!AppDataController.HasThemeSetting())
            {
                AppDataController.SetFirstRunStatus(3);
                parentFrame.Navigate(typeof(FirstRunTheme));
                return;
            }

            if (!AppDataController.HasUpdateServiceSetting())
            {
                AppDataController.SetFirstRunStatus(4);
                parentFrame.Navigate(typeof(FirstRunSourceSelect));
                return;
            }

            if (!AppDataController.HasWaveToolsHelper())
            {
                AppDataController.SetFirstRunStatus(5);
                parentFrame.Navigate(typeof(FirstRunGetDepend));
                return;
            }

            AppDataController.SetFirstRunStatus(6);
            parentFrame.Navigate(typeof(FirstRunExtra));
        }
    }
}
