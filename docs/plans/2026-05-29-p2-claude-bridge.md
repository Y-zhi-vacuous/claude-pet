# P2: 大脑阶段 — Claude 通信实现计划

> **Goal:** 小狗能通过 `claude -p` 发指令操控 Claude Code，能实时读取 transcript 显示 Claude 工作状态。

**Architecture:** ClaudeBridge 是核心协调器，内部持有 ProcessManager（管理 claude 子进程）和 TranscriptWatcher（监听 JSONL 文件）。PetWindow 通过事件订阅获取 StateSnapshot 更新，通过 `SendPrompt()` 发送用户指令。

**Tech Stack:** C# 12, WPF, System.Diagnostics.Process, FileSystemWatcher, Newtonsoft.Json

**Prerequisite:** P1 已完成

---

## 文件结构（P2 新增/修改）

```
新增:
  src/ClaudePet/Bridge/
    ├── ClaudeBridge.cs        # 主协调器，对外 API
    ├── ProcessManager.cs      # claude 子进程管理
    └── TranscriptWatcher.cs   # JSONL 增量解析
  src/ClaudePet/UI/
    └── HudMiniBar.xaml/.cs    # HUD 迷你状态条

修改:
  src/ClaudePet/Models/StateSnapshot.cs   # 添加 tools/agents/todos 列表
  src/ClaudePet/UI/PetWindow.xaml/.cs     # 集成 ClaudeBridge + HudMiniBar
  src/ClaudePet/App.xaml.cs               # 注入 ClaudeBridge
```

---

### Task 1: 扩展现有模型

**Files:**
- Modify: `D:\dev\claude-pet\src\ClaudePet\Models\StateSnapshot.cs` — 添加 tools/agents/todos

**修改代码：**

```csharp
namespace ClaudePet.Models;

public class StateSnapshot
{
    public string Model { get; set; } = string.Empty;
    public double ContextPercent { get; set; }
    public int InputTokens { get; set; }
    public int MaxTokens { get; set; }
    public string Status { get; set; } = "idle";
    public List<ToolActivity> ActiveTools { get; set; } = new();
    public List<AgentInfo> RunningAgents { get; set; } = new();
    public List<TodoItem> Todos { get; set; } = new();
}

public class ToolActivity
{
    public string ToolName { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;  // e.g. "auth.ts"
    public string State { get; set; } = "running";       // "running" | "done"
    public DateTime StartedAt { get; set; }
}

public class AgentInfo
{
    public string AgentName { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
}

public class TodoItem
{
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";  // "pending" | "in_progress" | "completed"
}
```

**验证：** 编译通过。

---

### Task 2: 创建 ProcessManager

**Files:**
- Create: `D:\dev\claude-pet\src\ClaudePet\Bridge\ProcessManager.cs`

**完整代码：**

```csharp
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
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    public ProcessManager(string workingDir)
    {
        _workingDir = workingDir;
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
            FileName = "claude",
            Arguments = $"-p \"{EscapeArg(prompt)}\" --continue",
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
                tcs.TrySetException(new Exception(
                    $"claude exited with code {process.ExitCode}: {stderr}"));
            process.Dispose();
        };

        ct.Register(() =>
        {
            try { process.Kill(); } catch { }
            tcs.TrySetCanceled();
        });

        process.Start();
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
```

**验证：** 编译通过。

---

### Task 3: 创建 TranscriptWatcher

**Files:**
- Create: `D:\dev\claude-pet\src\ClaudePet\Bridge\TranscriptWatcher.cs`

**完整代码：**

```csharp
using System.IO;
using Newtonsoft.Json.Linq;
using ClaudePet.Models;

namespace ClaudePet.Bridge;

/// <summary>
/// 监听 Claude Code 的 transcript JSONL 文件，解析新增行，推送 StateSnapshot。
/// </summary>
public class TranscriptWatcher : IDisposable
{
    private FileSystemWatcher? _watcher;
    private string? _currentFile;
    private long _lastPosition;
    private bool _disposed;

    public event Action<StateSnapshot>? StateUpdated;

    /// <summary>
    /// 启动监听。自动查找最新的 transcript 文件。
    /// </summary>
    public void Start(string transcriptsDir)
    {
        if (!Directory.Exists(transcriptsDir))
        {
            Directory.CreateDirectory(transcriptsDir);
        }

        var latest = FindLatestTranscript(transcriptsDir);
        if (latest != null)
        {
            _currentFile = latest;
            _lastPosition = new FileInfo(latest).Length;
        }

        _watcher = new FileSystemWatcher(transcriptsDir, "*.jsonl")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileCreated;
    }

    /// <summary>
    /// 手动读取一次当前 transcript 的增量，立即推送快照。
    /// </summary>
    public StateSnapshot ReadNow()
    {
        if (_currentFile == null || !File.Exists(_currentFile))
            return new StateSnapshot();

        return ParseIncremental(_currentFile);
    }

    private string? FindLatestTranscript(string dir)
    {
        return Directory.GetFiles(dir, "*.jsonl")
            .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc)
            .FirstOrDefault();
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_disposed) return;
        _currentFile = e.FullPath;
        var snapshot = ParseIncremental(e.FullPath);
        StateUpdated?.Invoke(snapshot);
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        if (_disposed) return;
        _currentFile = e.FullPath;
        _lastPosition = 0;
        var snapshot = ParseIncremental(e.FullPath);
        StateUpdated?.Invoke(snapshot);
    }

    private StateSnapshot ParseIncremental(string path)
    {
        var snapshot = new StateSnapshot();
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (_lastPosition > fs.Length) _lastPosition = 0; // 文件被截断了
            fs.Seek(_lastPosition, SeekOrigin.Begin);

            using var reader = new StreamReader(fs);
            while (reader.ReadLine() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                ParseLine(line, snapshot);
            }

            _lastPosition = fs.Position;
        }
        catch (IOException)
        {
            // 文件被锁定，跳过本次读取
        }
        return snapshot;
    }

    private static void ParseLine(string line, StateSnapshot snapshot)
    {
        try
        {
            var obj = JObject.Parse(line);
            var type = obj["type"]?.ToString();

            switch (type)
            {
                case "system":
                    if (obj["model"] != null)
                        snapshot.Model = obj["model"]!.ToString();
                    break;

                case "assistant":
                    if (obj["message"]?["usage"] is JObject usage)
                    {
                        snapshot.InputTokens = usage["input_tokens"]?.Value<int>() ?? 0;
                        snapshot.MaxTokens = 200000; // 默认 200k，后续可从 system 消息中取
                        snapshot.ContextPercent = snapshot.MaxTokens > 0
                            ? (double)snapshot.InputTokens / snapshot.MaxTokens * 100
                            : 0;
                    }
                    snapshot.Status = "working";
                    break;

                case "tool_use":
                    var toolName = obj["tool"]?.ToString() ?? "unknown";
                    var detail = "";
                    if (obj["input"] is JObject input)
                    {
                        detail = input["file_path"]?.ToString()
                              ?? input["pattern"]?.ToString()
                              ?? "";
                    }
                    snapshot.ActiveTools.Add(new ToolActivity
                    {
                        ToolName = toolName,
                        Detail = detail,
                        State = "running",
                        StartedAt = DateTime.UtcNow
                    });
                    break;

                case "tool_result":
                    var resultTool = obj["tool"]?.ToString() ?? "";
                    var existing = snapshot.ActiveTools
                        .FirstOrDefault(t => t.ToolName == resultTool && t.State == "running");
                    if (existing != null)
                        existing.State = "done";
                    break;

                case "task":
                    if (obj["subagent_type"] != null)
                    {
                        snapshot.RunningAgents.Add(new AgentInfo
                        {
                            AgentName = obj["subagent_type"]?.ToString() ?? "unknown",
                            Model = obj["model"]?.ToString() ?? "",
                            Description = obj["description"]?.ToString() ?? "",
                            StartedAt = DateTime.UtcNow
                        });
                    }
                    break;

                case "todo":
                    snapshot.Todos.Clear();
                    if (obj["todos"] is JArray todos)
                    {
                        foreach (var t in todos)
                        {
                            snapshot.Todos.Add(new TodoItem
                            {
                                Content = t["content"]?.ToString() ?? "",
                                Status = t["status"]?.ToString() ?? "pending"
                            });
                        }
                    }
                    break;
            }
        }
        catch
        {
            // 忽略解析失败的 JSON 行
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _watcher?.Dispose();
    }
}
```

**验证：** 编译通过。

---

### Task 4: 创建 ClaudeBridge（协调器）

**Files:**
- Create: `D:\dev\claude-pet\src\ClaudePet\Bridge\ClaudeBridge.cs`

**完整代码：**

```csharp
using ClaudePet.Models;

namespace ClaudePet.Bridge;

/// <summary>
/// Claude Code 通信总协调器。对外暴露 SendPrompt（发指令）和 StateUpdated 事件（读状态）。
/// </summary>
public class ClaudeBridge : IDisposable
{
    private readonly ProcessManager _processManager;
    private readonly TranscriptWatcher _transcriptWatcher;
    private StateSnapshot _lastSnapshot = new();
    private bool _disposed;

    public event Action<StateSnapshot>? StateUpdated;

    public ClaudeBridge(string workingDir, string transcriptsDir)
    {
        _processManager = new ProcessManager(workingDir);
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
            // 发完指令后立即刷新一次 transcript 以获取最终状态
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
    /// 主动触发一次状态读取（用于初次加载或定时轮询补充）。
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
```

**验证：** 编译通过。

---

### Task 5: 创建 HudMiniBar

**Files:**
- Create: `D:\dev\claude-pet\src\ClaudePet\UI\HudMiniBar.xaml`
- Create: `D:\dev\claude-pet\src\ClaudePet\UI\HudMiniBar.xaml.cs`

**HudMiniBar.xaml：**

```xml
<UserControl x:Class="ClaudePet.UI.HudMiniBar"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Width="180" Height="40">
    <Border Background="#E0000000" CornerRadius="6" Padding="6,3">
        <StackPanel>
            <TextBlock x:Name="ModelLabel"
                       FontSize="10" Foreground="#AAAAAA"
                       Text="等待连接..." />
            <Grid Height="6" Margin="0,2,0,0">
                <Rectangle x:Name="ContextBg" Fill="#333333" RadiusX="3" RadiusY="3" />
                <Rectangle x:Name="ContextBar" Fill="#4CAF50" RadiusX="3" RadiusY="3"
                           HorizontalAlignment="Left" Width="0" />
            </Grid>
        </StackPanel>
    </Border>
</UserControl>
```

**HudMiniBar.xaml.cs：**

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClaudePet.Models;

namespace ClaudePet.UI;

public partial class HudMiniBar : UserControl
{
    public HudMiniBar()
    {
        InitializeComponent();
    }

    public void UpdateState(StateSnapshot state)
    {
        Dispatcher.Invoke(() =>
        {
            var model = string.IsNullOrEmpty(state.Model) ? "等待连接..." : state.Model;
            ModelLabel.Text = state.Status switch
            {
                "working" => $"[{model}] ● 工作中...",
                "error"   => $"[{model}] ✘ 出错",
                _         => $"[{model}] ✓ 就绪"
            };

            var pct = Math.Clamp(state.ContextPercent, 0, 100);
            ContextBar.Width = pct / 100.0 * 168; // 180 - 12 padding

            ContextBar.Fill = pct switch
            {
                >= 85 => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)), // red
                >= 70 => new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)), // yellow
                _      => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))  // green
            };

            ModelLabel.Text += $" | Context {pct:F0}%";
        });
    }
}
```

**验证：** 编译通过。

---

### Task 6: 修改 PetWindow 集成 ClaudeBridge + HudMiniBar

**Files:**
- Modify: `D:\dev\claude-pet\src\ClaudePet\UI\PetWindow.xaml`
- Modify: `D:\dev\claude-pet\src\ClaudePet\UI\PetWindow.xaml.cs`
- Modify: `D:\dev\claude-pet\src\ClaudePet\App.xaml.cs`

**PetWindow.xaml 修改（在 Canvas 后加 HudMiniBar）：**

在 `</Canvas>` 后、`</Window>` 前加：
```xml
    <claudePet:HudMiniBar x:Name="HudBar"
                           VerticalAlignment="Bottom"
                           HorizontalAlignment="Center"
                           Margin="0,0,0,8" />
```

同时在 Window 标签加命名空间：`xmlns:claudePet="clr-namespace:ClaudePet.UI"`

**PetWindow.xaml.cs 修改：**

构造器签名改为接受 `ClaudeBridge`：
```csharp
private readonly ClaudeBridge _bridge;

public PetWindow(ConfigStore config, ClaudeBridge bridge)
{
    _config = config;
    _bridge = bridge;
    _dragHelper = new MouseDragHelper(this);
    InitializeComponent();
}
```

`Window_Loaded` 末尾加：
```csharp
    _bridge.StateUpdated += OnClaudeStateUpdated;
    var initial = _bridge.GetCurrentState();
    HudBar.UpdateState(initial);
```

新增方法：
```csharp
private void OnClaudeStateUpdated(StateSnapshot state)
{
    Dispatcher.Invoke(() => HudBar.UpdateState(state));
}
```

**App.xaml.cs 修改：**

`Application_Startup` 中改为先创建 bridge 再传给窗口：
```csharp
// transcripts 目录通常在 ~/.claude/projects/<project-hash>/
var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var transcriptsDir = Path.Combine(home, ".claude", "projects");

var bridge = new ClaudeBridge(baseDir, transcriptsDir);

_trayManager = new TrayManager();
_petWindow = new PetWindow(config, bridge);
_petWindow.Show();
```

**验证：** 编译通过。

---

### Task 7: 修改 config.json 增加 workingDir

**Files:**
- Modify: `D:\dev\claude-pet\config.json`

添加：
```json
{
  "petName": "旺财",
  "windowLeft": -1,
  "windowTop": -1,
  "animationPaused": false,
  "voiceEnabled": false,
  "wakeWord": "嘿小狗",
  "idleSleepMinutes": 5,
  "workingDirectory": ".",
  "transcriptsDirectory": ""
}
```

同时也需要修改 PetConfig.cs 加入这两个字段。

---

## 验证检查单

P2 完成后的手动验证：

- [ ] 启动小狗，HUD 状态条显示在方块下方
- [ ] HUD 显示当前模型名称或"等待连接..."
- [ ] 右键点击小狗选择"发送测试"，能向 Claude 发一条测试消息
- [ ] Claude 处理期间，HUD 显示 "● 工作中..."
- [ ] Claude 完成回复后，HUD 恢复 "✓ 就绪"
- [ ] HUD 上下文进度条正确显示绿色/黄色/红色
