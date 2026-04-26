using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Agent1;

public partial class MainWindow : Window
{
    private readonly IntPtr _targetWindow;
    private AgentController? _controller;
    private Exception? _initializationException;
    private int _closeRequested;

    public MainWindow(IntPtr targetWindow)
    {
        InitializeComponent();
        _targetWindow = targetWindow;

        Left = SystemParameters.WorkArea.Right - Width - 24;
        Top = SystemParameters.WorkArea.Top + 24;
        Loaded += MainWindow_Loaded;
        SourceInitialized += MainWindow_SourceInitialized;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE).ToInt64();
            style |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_NOACTIVATE;
            NativeMethods.SetLastError(0);
            var previous = NativeMethods.SetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
            if (previous == IntPtr.Zero && Marshal.GetLastWin32Error() != 0)
            {
                throw new InvalidOperationException("SetWindowLongPtr не смог настроить окно статуса.");
            }
        }
        catch (Exception ex)
        {
            _initializationException = ex;
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_initializationException is not null)
            {
                throw _initializationException;
            }

            _controller = new AgentController(_targetWindow, SetStatus, HideStatusWindow);
            await _controller.RunAsync();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Работа агента остановлена.");
            RequestClose();
        }
        catch (Exception ex)
        {
            ShowStatusWindow();
            SetStatus("Ошибка агента: " + ex.Message);
            RequestClose();
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        Interlocked.Exchange(ref _closeRequested, 1);
        _controller?.Dispose();
    }

    private void RequestClose()
    {
        if (Interlocked.Exchange(ref _closeRequested, 1) == 1)
        {
            return;
        }

        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        try
        {
            Close();
        }
        catch
        {
        }
    }

    private void SetStatus(string message)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        try
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                {
                    StatusText.Text = message;
                }
            }));
        }
        catch
        {
        }
    }

    private void HideStatusWindow()
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        try
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                {
                    Hide();
                }
            }));
        }
        catch
        {
        }
    }

    private void ShowStatusWindow()
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        try
        {
            Show();
        }
        catch
        {
        }
    }
}
