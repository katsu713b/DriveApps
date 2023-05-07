using DriveApp.Dash.PFC;
using System.ComponentModel;
using System.Windows;

namespace DriveApp.Dash.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
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
        _dash.Show();
    }

    private void InitializeTimer()
    {
        this.Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
    }
}

public class MainWindowVM : ViewModelBase
{
    private string _title = "MainWindow";
    public string Title
    {
        get { return _title; }
        set { if (_title != value) { _title = value; RaisePropertyChanged(); } }
    }
}
