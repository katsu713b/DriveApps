using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DriveApp.Dash.UI;

public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged = null;

    // 変更通知
    public void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}