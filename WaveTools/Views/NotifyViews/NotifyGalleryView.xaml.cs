using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using WaveTools.Depend;
using static WaveTools.Views.MainView;

namespace WaveTools.Views.NotifyViews
{
    public sealed partial class NotifyGalleryView : Page
    {
        public ObservableCollection<string> Pictures { get; } = new ObservableCollection<string>();
        public List<string> JumpUrls { get; private set; }

        public NotifyGalleryView()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is GalleryNavigationData navData)
            {
                Pictures.Clear();
                foreach (var pic in navData.Pictures)
                {
                    Pictures.Add(pic);
                }
                JumpUrls = navData.JumpUrls;
            }
        }

        private void Gallery_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FlipView flipView)
            {
                int selectedPicture = flipView.SelectedIndex;
                if (JumpUrls != null && selectedPicture >= 0 && selectedPicture < JumpUrls.Count)
                {
                    string url = JumpUrls[selectedPicture];
                    if (!string.IsNullOrEmpty(url))
                    {
                        Logging.Write("Opening URL from gallery: " + url, 0);
                        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                    }
                }
            }
        }
    }
}