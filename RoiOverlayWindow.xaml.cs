using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Agent1;

public partial class RoiOverlayWindow : Window
{
    private readonly TaskCompletionSource<ScreenRectangle> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Point _start;
    private bool _isDragging;
    private int _completed;

    private RoiOverlayWindow()
    {
        InitializeComponent();

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        MouseDown += Overlay_MouseDown;
        MouseMove += Overlay_MouseMove;
        MouseUp += Overlay_MouseUp;
    }

    public static async Task<ScreenRectangle> SelectAsync(CancellationToken token)
    {
        var overlay = new RoiOverlayWindow();
        using var registration = token.Register(() =>
        {
            if (Interlocked.Exchange(ref overlay._completed, 1) == 1)
            {
                return;
            }

            overlay._result.TrySetCanceled(token);

            if (overlay.Dispatcher.HasShutdownStarted || overlay.Dispatcher.HasShutdownFinished)
            {
                return;
            }

            try
            {
                overlay.Dispatcher.BeginInvoke(new Action(() =>
                {
                    overlay.Close();
                }));
            }
            catch
            {
            }
        });

        overlay.ShowDialog();
        return await overlay._result.Task.ConfigureAwait(true);
    }

    private void Overlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _start = e.GetPosition(RootCanvas);
        _isDragging = true;
        CaptureMouse();
        SelectionBorder.Visibility = Visibility.Visible;
        UpdateSelection(_start, _start);
    }

    private void Overlay_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        UpdateSelection(_start, e.GetPosition(RootCanvas));
    }

    private void Overlay_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging || e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _isDragging = false;
        ReleaseMouseCapture();

        var end = e.GetPosition(RootCanvas);
        var screenStart = PointToScreen(_start);
        var screenEnd = PointToScreen(end);

        var x1 = (int)Math.Round(Math.Min(screenStart.X, screenEnd.X));
        var y1 = (int)Math.Round(Math.Min(screenStart.Y, screenEnd.Y));
        var x2 = (int)Math.Round(Math.Max(screenStart.X, screenEnd.X));
        var y2 = (int)Math.Round(Math.Max(screenStart.Y, screenEnd.Y));
        var width = Math.Max(1, x2 - x1);
        var height = Math.Max(1, y2 - y1);

        if (Interlocked.Exchange(ref _completed, 1) == 1)
        {
            return;
        }

        _result.TrySetResult(new ScreenRectangle(x1, y1, width, height));
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (Interlocked.Exchange(ref _completed, 1) == 0)
        {
            _result.TrySetCanceled();
        }

        base.OnClosed(e);
    }

    private void UpdateSelection(Point a, Point b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        var width = Math.Abs(a.X - b.X);
        var height = Math.Abs(a.Y - b.Y);

        Canvas.SetLeft(SelectionBorder, x);
        Canvas.SetTop(SelectionBorder, y);
        SelectionBorder.Width = width;
        SelectionBorder.Height = height;
    }
}
