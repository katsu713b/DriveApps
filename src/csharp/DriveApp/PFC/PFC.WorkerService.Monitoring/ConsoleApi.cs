using System.Runtime.InteropServices;

namespace PFC.WorkerService.Monitoring;

/// <summary>
/// ConsoleAPIクラス
/// https://www.exceedsystem.net/2021/02/13/how-to-handle-console-application-close-events-like-winforms-formclosed-event/
/// </summary>
public sealed class ConsoleAPI
{
    // https://docs.microsoft.com/en-us/windows/console/handlerroutine
    public delegate bool ConsoleCtrlDelegate(CtrlTypes CtrlType);

    // https://docs.microsoft.com/en-us/windows/console/handlerroutine
    public enum CtrlTypes : uint
    {
        CTRL_C_EVENT = 0,
        CTRL_BREAK_EVENT = 1,
        CTRL_CLOSE_EVENT = 2,
        CTRL_LOGOFF_EVENT = 5,
        CTRL_SHUTDOWN_EVENT = 6
    }

    // https://docs.microsoft.com/en-us/windows/console/setconsolectrlhandler
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate handlerRoutine, bool add);
}
