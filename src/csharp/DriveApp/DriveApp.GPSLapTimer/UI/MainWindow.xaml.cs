using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Management;
using System.Diagnostics;
using System.Timers;
using System.Globalization;

namespace DriveApp.GPSLapTimer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    ManagementClass mcW32SerPort = new ManagementClass("Win32_SerialPort");
    private ViewModel _viewModel = new ViewModel();
    private Timer _timer = new Timer(3000);
    public MainWindow()
    {
        InitializeComponent();

        // Add an event handler to update canvas background color just before it is rendered.
        //CompositionTarget.Rendering += UpdateColor;

        
        
        _frameCounter = 0;
        _stopwatch.Restart();
        DataContext = _viewModel;
    }
    
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        string testString = "Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor";

        // Create the initial formatted text string.
        FormattedText formattedText = new FormattedText(
            testString,
            CultureInfo.GetCultureInfo("en-us"),
            FlowDirection.LeftToRight,
            new Typeface("Verdana"),
            32,
            Brushes.Black, 48);

        // Set a maximum width and height. If the text overflows these values, an ellipsis "..." appears.
        formattedText.MaxTextWidth = 300;
        formattedText.MaxTextHeight = 240;

        // Use a larger font size beginning at the first (zero-based) character and continuing for 5 characters.
        // The font size is calculated in terms of points -- not as device-independent pixels.
        formattedText.SetFontSize(36 * (96.0 / 72.0), 0, 5);

        // Use a Bold font weight beginning at the 6th character and continuing for 11 characters.
        formattedText.SetFontWeight(FontWeights.Bold, 6, 11);

        // Use a linear gradient brush beginning at the 6th character and continuing for 11 characters.
        formattedText.SetForegroundBrush(
                                new LinearGradientBrush(
                                Colors.Orange,
                                Colors.Teal,
                                90.0),
                                6, 11);

        // Use an Italic font style beginning at the 28th character and continuing for 28 characters.
        formattedText.SetFontStyle(FontStyles.Italic, 28, 28);

        // Draw the formatted text string to the DrawingContext of the control.
        drawingContext.DrawText(formattedText, new Point(10, 0));
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        foreach (ManagementObject port in mcW32SerPort.GetInstances())
        {
            _viewModel.Message += port.GetPropertyValue("Caption") + "\r\n"; // Communications Port (COM1)
            _viewModel.Message += port.GetPropertyValue("DeviceID") + "\r\n"; // COM1
        }

        //string.Join("\r\n", SerialPort.GetPortNames());
        
        

    }
    private int _frameCounter = 0;
    private Stopwatch _stopwatch = new Stopwatch();

    private static void TimerFps(object? state)
    {
        throw new NotImplementedException();
    }

    // Called just before frame is rendered to allow custom drawing.
    protected void UpdateColor(object sender, EventArgs e)
    {
        _frameCounter++;
            long frameRate = (long)(_frameCounter / this._stopwatch.Elapsed.TotalSeconds);
            if (frameRate < 0) return;
            _viewModel.Message = frameRate.ToString();


        // Determine frame rate in fps (frames per second).
        // long frameRate = (long)(_frameCounter / this._stopwatch.Elapsed.TotalSeconds);
        //if (frameRate > 0)
        //{
            // Update elapsed time, number of frames, and frame rate.
            //myStopwatchLabel.Content = _stopwatch.Elapsed.ToString();
            //myFrameCounterLabel.Content = _frameCounter.ToString();
            //_viewModel.Message = frameRate.ToString();
        //}

        // Update the background of the canvas by converting MouseMove info to RGB info.
        //byte redColor = (byte)(_pt.X / 3.0);
        //byte blueColor = (byte)(_pt.Y / 2.0);
        //myCanvas.Background = new SolidColorBrush(Color.FromRgb(redColor, 0x0, blueColor));
    }
}

public class ViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    // 変更通知
    public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

   
    private string _Message;
    public string Message
    {
        get { return _Message; }
        set { if (_Message != value) { _Message = value; RaisePropertyChanged(); } }
    }

}