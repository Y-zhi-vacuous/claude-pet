using ClaudePet.Models;

namespace ClaudePet.Bridge;

/// <summary>
/// Claude Code 通信总协调器。对外暴露 SendPrompt 和 StateUpdated 事件。
/// </summary>
public class ClaudeBridge : IDisposable
{
    private readonly ProcessManager _processManager;
    private readonly TranscriptWatcher _transcriptWatcher;
    private StateSnapshot _lastSnapshot = new();
    private bool _disposed;

    public event Action<StateSnapshot>? StateUpdated;

    public ClaudeBridge(string workingDir, string transcriptsDir, string claudePath)
    {
        _processManager = new ProcessManager(workingDir, claudePath);
        _transcriptWatcher = new TranscriptWatcher();
        _transcriptWatcher.StateUpdated += OnStateUpdated;
        _transcriptWatcher.Start(transcriptsDir);
    }

    /// <summary>
    /// 发送用户的自然语言指令给 Claude Code，返回 Claude 的回复文本。
    /// </summary>
    public async Task<string> SendPrompt(string prompt)
    {
        OnStateUpdated(new StateSnapshot { Status = "working" });
        try
        {
            var result = await _processManager.SendPrompt(prompt);
            var final = _transcriptWatcher.ReadNow();
            final.Status = "idle";
            OnStateUpdated(final);
            return result;
        }
        catch (Exception ex)
        {
            OnStateUpdated(new StateSnapshot { Status = "error" });
            return $"错误: {ex.Message}";
        }
    }

    /// <summary>
    /// 主动触发一次状态读取。
    /// </summary>
    public StateSnapshot GetCurrentState()
    {
        return _transcriptWatcher.ReadNow();
    }

    private void OnStateUpdated(StateSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        StateUpdated?.Invoke(snapshot);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _processManager.Dispose();
        _transcriptWatcher.Dispose();
    }
}
