using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace ClaudePet.Voice;

/// <summary>
/// 全局键盘热键唤醒：Ctrl+Shift+W 模拟"嘿小狗"唤醒词。
/// 使用 Win32 GetAsyncKeyState 实现全局按键检测（不需要窗口焦点）。
/// </summary>
public class KeyboardWakeDetector : IWakeWordDetector
{
    private DispatcherTimer? _timer;
    private bool _disposed;
    private DateTime _lastTrigger = DateTime.MinValue;

    // Win32 API — 全局检测按键状态
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_CONTROL = 0x11;
    private const int VK_SHIFT = 0x10;
    private const int VK_W = 0x57;

    public event Action? WakeWordDetected;

    public void Start()
    {
        if (_timer != null) return;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_disposed) return;

        try
        {
            // 用 Win32 API 检测全局按键状态（不需要窗口焦点）
            bool ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            bool shift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
            bool w = (GetAsyncKeyState(VK_W) & 0x8000) != 0;

            if (ctrl && shift && w)
            {
                if ((DateTime.Now - _lastTrigger).TotalSeconds > 2)
                {
                    _lastTrigger = DateTime.Now;
                    WakeWordDetected?.Invoke();
                }
            }
        }
        catch
        {
            // 忽略异常
        }
    }

    public void Dispose()
    {
        _disposed = true;
        Stop();
    }
}
