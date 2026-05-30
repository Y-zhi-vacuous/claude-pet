namespace ClaudePet.Voice;

public enum VoiceState { Listening, WakedUp, Speaking }

/// <summary>
/// 语音引擎：Ctrl+Shift+W → 开聊天窗，TTS 朗读回复。
/// </summary>
public class VoiceEngine : IDisposable
{
    private readonly IWakeWordDetector _wakeDetector;
    private bool _enabled;
    private bool _disposed;

    public event Action? ChatRequested;
    public event Action<VoiceState>? StateChanged;

    public VoiceEngine()
    {
        _wakeDetector = new KeyboardWakeDetector();
        _wakeDetector.WakeWordDetected += () =>
        {
            if (_enabled && !_disposed)
            {
                SetState(VoiceState.WakedUp);
                ChatRequested?.Invoke();
            }
        };
    }

    public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; if (value) _wakeDetector.Start(); else _wakeDetector.Stop(); }
    }

    public async Task SpeakAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            SetState(VoiceState.Speaking);
            using var tts = new EdgeTTSEngine();
            await tts.SpeakAsync(text);
        }
        catch { }
        SetState(VoiceState.Listening);
    }

    private void SetState(VoiceState s) => StateChanged?.Invoke(s);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _wakeDetector.Dispose();
    }
}
