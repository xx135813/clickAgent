using System.Windows;

namespace Agent1;

internal sealed class AgentController : IDisposable
{
    private const int QuantizedStep = 8;
    private static readonly TimeSpan InitialPause = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshPause = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ClickPause = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RoiSelectionPause = TimeSpan.FromSeconds(2);

    private readonly IntPtr _targetWindow;
    private readonly Action<string> _status;
    private readonly Action _hideStatusWindow;
    private readonly GlobalHookService _hooks;
    private readonly ScreenCaptureService _capture = new();
    private readonly InputService _input = new();
    private readonly List<ScreenPoint> _clicks = new(capacity: 4);
    private readonly CancellationTokenSource _escCancellation = new();
    private ScreenRectangle _roi;
    private bool _disposed;
    private int _stopRequested;

    public AgentController(IntPtr targetWindow, Action<string> status, Action hideStatusWindow)
    {
        _targetWindow = targetWindow;
        _status = status;
        _hideStatusWindow = hideStatusWindow;
        _hooks = new GlobalHookService();
        _hooks.EscapePressed += StopFromEscape;
    }

    public async Task RunAsync()
    {
        if (_targetWindow == IntPtr.Zero)
        {
            throw new InvalidOperationException("target_window не определён: GetForegroundWindow вернул пустой handle.");
        }

        var token = _escCancellation.Token;

        _hooks.Start();
        _status("Передайте 4 клика. Клики проходят в браузер, агент только запоминает координаты.");

        for (var i = 1; i <= 4; i++)
        {
            var point = await WaitForClickAsync(i, token).ConfigureAwait(true);
            _clicks.Add(point);
            _status($"Клик{i} сохранён: X={point.X}, Y={point.Y}.");
        }

        _status("Выделите ROI прямоугольником мышью. На этом этапе клики не передаются в браузер.");
        _roi = await RoiOverlayWindow.SelectAsync(token).ConfigureAwait(true);
        _status($"ROI сохранён: X={_roi.X}, Y={_roi.Y}, W={_roi.Width}, H={_roi.Height}. Пауза 2 секунды.");
        _hideStatusWindow();
        await Task.Delay(RoiSelectionPause, token).ConfigureAwait(true);

        var referenceColors = await CaptureQuantizedColorsAsync(token).ConfigureAwait(true);
        _status($"Эталонный набор сохранён. Уникальных квантованных цветов: {referenceColors.Count}. Старт основного цикла.");

        while (true)
        {
            await DelayWithStatusAsync("Стоп 15 минут перед обновлением страницы.", InitialPause, token).ConfigureAwait(true);

            FocusTargetWindow();
            _status("Обновление target_window через F5. Стоп 10 секунд.");
            _input.SendF5();
            await Task.Delay(RefreshPause, token).ConfigureAwait(true);

            await ClickSavedPointAsync(1, token).ConfigureAwait(true);
            await ClickSavedPointAsync(2, token).ConfigureAwait(true);
            await ClickSavedPointAsync(3, token).ConfigureAwait(true);

            for (var i = 1; i <= 25; i++)
            {
                token.ThrowIfCancellationRequested();
                _status($"Цикл проверки {i}/25: снимок ROI и сравнение цветов.");

                var newColors = await CaptureQuantizedColorsAsync(token).ConfigureAwait(true);
                if (ContainsNewColor(newColors, referenceColors))
                {
                    _status("Обнаружен новый квантованный цвет. Запуск 500 beep и завершение.");
                    await BeepAndShutdownAsync(token).ConfigureAwait(true);
                    Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
                    return;
                }

                await ClickSavedPointAsync(4, token).ConfigureAwait(true);
            }
        }
    }

    private async Task<ScreenPoint> WaitForClickAsync(int clickNumber, CancellationToken token)
    {
        _status($"Выполните клик{clickNumber} мышью.");

        var tcs = new TaskCompletionSource<ScreenPoint>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(ScreenPoint point)
        {
            _hooks.LeftMouseDown -= Handler;
            tcs.TrySetResult(point);
        }

        _hooks.LeftMouseDown += Handler;
        using var registration = token.Register(() =>
        {
            _hooks.LeftMouseDown -= Handler;
            tcs.TrySetCanceled(token);
        });

        return await tcs.Task.ConfigureAwait(true);
    }

    private Task<HashSet<int>> CaptureQuantizedColorsAsync(CancellationToken token)
    {
        return Task.Run(() => CaptureQuantizedColors(token), token);
    }

    private HashSet<int> CaptureQuantizedColors(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var pixels = _capture.CaptureRgb24(_roi);
        token.ThrowIfCancellationRequested();
        return ColorQuantizer.GetUniqueQuantizedColors(pixels, QuantizedStep, token);
    }

    private bool ContainsNewColor(HashSet<int> newColors, HashSet<int> referenceColors)
    {
        foreach (var color in newColors)
        {
            if (!referenceColors.Contains(color))
            {
                return true;
            }
        }

        return false;
    }

    private async Task ClickSavedPointAsync(int index, CancellationToken token)
    {
        var point = _clicks[index - 1];
        _status($"Выполняется клик{index}: X={point.X}, Y={point.Y}. Стоп 3 секунды.");
        FocusTargetWindow();
        _input.Click(point);
        await Task.Delay(ClickPause, token).ConfigureAwait(true);
    }

    private async Task DelayWithStatusAsync(string message, TimeSpan duration, CancellationToken token)
    {
        _status(message);
        await Task.Delay(duration, token).ConfigureAwait(true);
    }

    private async Task BeepAndShutdownAsync(CancellationToken token)
    {
        for (var i = 1; i <= 500; i++)
        {
            token.ThrowIfCancellationRequested();
            await Task.Run(() => NativeMethods.Beep(1000, 120), token).ConfigureAwait(true);
        }
    }

    private void FocusTargetWindow()
    {
        if (!NativeMethods.SetForegroundWindow(_targetWindow))
        {
            throw new InvalidOperationException("SetForegroundWindow не смог вернуть фокус target_window.");
        }
    }

    private void StopFromEscape()
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) == 1)
        {
            return;
        }

        try
        {
            _status("Esc нажат. Немедленная остановка агента.");
        }
        catch
        {
        }

        try
        {
            _escCancellation.Cancel();
        }
        catch
        {
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        try
        {
            dispatcher.BeginInvoke(new Action(() => Application.Current.Shutdown()));
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _escCancellation.Cancel();
        _hooks.EscapePressed -= StopFromEscape;
        _hooks.Dispose();
        _escCancellation.Dispose();
        _disposed = true;
    }
}
