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
            if (_lastPosition > fs.Length) _lastPosition = 0;
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
                        snapshot.MaxTokens = 200000;
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
