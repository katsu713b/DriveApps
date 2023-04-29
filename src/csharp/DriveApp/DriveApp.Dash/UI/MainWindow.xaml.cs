using DriveApp.Dash.PFC;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace DriveApp.Dash.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private DispatcherTimer _timer;
    private MainWindowVM _dataContext;
    private DashWindow _dash;
    private PFCContext _pFCData;

    public MainWindow(DashWindow dashWindow, PFCContext pFCData)
    {
        _dash = dashWindow;
        _pFCData = pFCData;

        ContentRendered += MainWindow_ContentRendered;
        InitializeComponent();

        _dataContext = (MainWindowVM)this.DataContext;

        InitializeTimer();
    }

    private void MainWindow_ContentRendered(object? sender, EventArgs e)
    {
        _timer.Start();

        _dash.Show();
    }

    private void InitializeTimer()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Normal);
        _timer.Interval = TimeSpan.FromMilliseconds(17);//17
        _timer.Tick += _timer_Tick;
        this.Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        _timer.Stop();
    }

    private void _timer_Tick(object? sender, EventArgs e)
    {
        
        _dataContext.Title = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        //title.Content = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        //System.Diagnostics.Debug.WriteLine(a.Title);
    }

}

public class MainWindowVM : ViewModelBase
{
    private string _title;
    public string Title
    {
        get { return _title; }
        set { if (_title != value) { _title = value; RaisePropertyChanged(); } }
    }
}
