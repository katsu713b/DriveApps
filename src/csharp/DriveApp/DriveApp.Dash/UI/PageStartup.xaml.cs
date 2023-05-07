using System;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace DriveApp.Dash.UI
{
    /// <summary>
    /// PageStartup.xaml の相互作用ロジック
    /// </summary>
    public partial class PageStartup : Page
    {
        //DispatcherTimer _timer;
        public PageStartup()
        {
            InitializeComponent();

            //_timer = new DispatcherTimer();
            //_timer.Interval = TimeSpan.FromSeconds(5);
            //_timer.Tick += _timer_Tick;
            //_timer.Start();
        }


        private void _timer_Tick(object? sender, EventArgs e)
        {
            //_timer.Stop();
            NavigationService.Navigate(new PageSetting());
        }

    }
}
