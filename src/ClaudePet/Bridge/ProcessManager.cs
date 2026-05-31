using System.Diagnostics;
using System.Text;

namespace ClaudePet.Bridge;

/// <summary>
/// 管理 Claude Code CLI 子进程。
/// 每次 SendPrompt 启动一个短期进程：claude -p "prompt" --continue
/// </summary>
public class ProcessManager : IDisposable
{
    private readonly string _workingDir;
    private readonly string _claudePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// 查找 claude 可执行文件，优先用完整路径。
    /// </summary>
    public static string FindClaudePath()
    {
        // 常见位置
        var candidates = new List<string>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        candidates.Add(Path.Combine(home, "AppData", "Roaming", "npm", "claude.cmd"));
        candidates.Add(Path.Combine(home, "AppData", "Roaming", "npm", "claude"));
        candidates.Add(Path.Combine(home, "AppData", "Local", "npm", "claude.cmd"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "claude.cmd"));

        // 也搜 PATH
        var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';');
        foreach (var dir in pathDirs)
        {
            if (!string.IsNullOrWhiteSpace(dir))
            {
                candidates.Add(Path.Combine(dir.Trim(), "claude.cmd"));
                candidates.Add(Path.Combine(dir.Trim(), "claude"));
            }
        }

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        // fallback
        return "claude";
    }

    public ProcessManager(string workingDir, string claudePath)
    {
        _workingDir = workingDir;
        _claudePath = claudePath;
    }

    /// <summary>
    /// 发送 prompt 给 claude，阻塞等待完成，返回 stdout 输出。
    /// </summary>
    public async Task<string> SendPrompt(string prompt, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return await RunClaudeAsync(prompt, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private Task<string> RunClaudeAsync(string prompt, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<string>();
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var psi = new ProcessStartInfo
        {
            FileName = _claudePath,
            Arguments = $"-p \"{EscapeArg(prompt)}\" --permission-mode bypassPermissions",
            WorkingDirectory = _workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) stderr.AppendLine(e.Data);
        };

        process.Exited += (_, _) =>
        {
            if (process.ExitCode == 0)
                tcs.TrySetResult(stdout.ToString());
            else
            {
                var errText = stderr.ToString();
                var message = process.ExitCode switch
                {
                    _ when errText.Contains("command not found") || errText.Contains("not recognized")
                        => "找不到 claude 命令，请确认 Claude Code 已安装并在 PATH 中。",
                    _ => $"Claude 异常退出 (code {process.ExitCode})\n{errText.Trim()}"
                };
                tcs.TrySetException(new Exception(message));
            }
            process.Dispose();
        };

        ct.Register(() =>
        {
            try { process.Kill(); } catch { }
            tcs.TrySetCanceled();
        });

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            tcs.TrySetException(new Exception(
                $"无法启动 Claude ({_claudePath})，请检查是否已安装 Claude Code。\n{ex.Message}"));
            return tcs.Task;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return tcs.Task;
    }

    private static string EscapeArg(string arg) =>
        arg.Replace("\\", "\\\\").Replace("\"", "\\\"");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lock.Dispose();
    }
}
