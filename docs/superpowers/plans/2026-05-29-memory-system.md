# 千千记忆系统 — 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让千千拥有持久记忆——重启不丢失对话/任务/偏好，上下文过长自动压缩，空闲时自动整理。

**Architecture:** 在 ClaudeBridge 外围新增 MemoryEngine 组件（6 个新文件），拦截 SendPrompt 拼接记忆前缀，监控 TranscriptWatcher 触发压缩，空闲时自主调 Claude 整理记忆。纯 Markdown 文件存储，不改 ClaudeBridge 核心逻辑。

**Tech Stack:** C# 12, .NET 8, WPF, 纯文件 I/O（System.IO），Markdown 格式

**Prerequisite:** 设计文档 `docs/superpowers/specs/2026-05-29-memory-system-design.md`

---

## 文件结构总览

```
新增:
  src/ClaudePet/Memory/
    ├── MemoryModels.cs        # MemoryEntry / MemoryIndex 数据结构
    ├── MemoryLoader.cs        # 文件加载 + 索引构建
    ├── MemoryWriter.cs        # 文件写入 + 去重
    ├── ContextBuilder.cs      # prompt 前缀拼接
    ├── Compressor.cs          # 上下文压缩
    ├── IdleOrganizer.cs       # 空闲整理调度
    └── MemoryEngine.cs        # 总协调器

修改:
  src/ClaudePet/App.xaml.cs           # 创建 MemoryEngine，注入
  src/ClaudePet/UI/PetWindow.xaml.cs  # 改用 memoryEngine.SendWithMemory()
```

---

### Task 1: 创建 MemoryModels — 数据结构

**Files:**
- Create: `src/ClaudePet/Memory/MemoryModels.cs`

- [ ] **Step 1: 创建 MemoryModels.cs**

```csharp
namespace ClaudePet.Memory;

/// <summary>
/// 单条记忆条目。
/// </summary>
public class MemoryEntry
{
    public string Category { get; set; } = "";   // "task" | "preference" | "knowledge" | "conversation"
    public string Content { get; set; } = "";
    public string Source { get; set; } = "";      // 来源文件
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 内存中的记忆索引。
/// </summary>
public class MemoryIndex
{
    public List<MemoryEntry> Tasks { get; set; } = new();
    public List<MemoryEntry> Preferences { get; set; } = new();
    public List<MemoryEntry> Knowledge { get; set; } = new();
    public List<MemoryEntry> RecentConversations { get; set; } = new();
    public DateTime LastCompressAt { get; set; } = DateTime.MinValue;
    public DateTime LastOrganizeAt { get; set; } = DateTime.MinValue;
}
```

- [ ] **Step 2: 编译验证**

```bash
dotnet build claude-pet.sln
```

---

### Task 2: 创建 MemoryLoader — 文件加载 + 索引构建

**Files:**
- Create: `src/ClaudePet/Memory/MemoryLoader.cs`

- [ ] **Step 1: 创建 MemoryLoader.cs**

```csharp
namespace ClaudePet.Memory;

/// <summary>
/// 启动时从 memory/ 目录加载所有 .md 文件，构建 MemoryIndex。
/// </summary>
public class MemoryLoader
{
    private readonly string _memoryDir;

    public MemoryLoader(string memoryDir)
    {
        _memoryDir = memoryDir;
    }

    /// <summary>
    /// 加载所有记忆文件，构建内存索引。
    /// </summary>
    public MemoryIndex Load()
    {
        var index = new MemoryIndex();

        if (!Directory.Exists(_memoryDir))
        {
            Directory.CreateDirectory(_memoryDir);
            CreateDefaultFiles();
            return index;
        }

        // 读 preferences.md
        var prefsPath = Path.Combine(_memoryDir, "preferences.md");
        if (File.Exists(prefsPath))
        {
            foreach (var entry in ParseBulletFile(prefsPath, "preference"))
                index.Preferences.Add(entry);
        }

        // 读 knowledge.md
        var knowledgePath = Path.Combine(_memoryDir, "knowledge.md");
        if (File.Exists(knowledgePath))
        {
            foreach (var entry in ParseBulletFile(knowledgePath, "knowledge"))
                index.Knowledge.Add(entry);
        }

        // 读 tasks.md
        var tasksPath = Path.Combine(_memoryDir, "tasks.md");
        if (File.Exists(tasksPath))
        {
            foreach (var entry in ParseBulletFile(tasksPath, "task"))
                index.Tasks.Add(entry);
        }

        // 读 conversation/ 最近 3 天
        var convDir = Path.Combine(_memoryDir, "conversation");
        if (Directory.Exists(convDir))
        {
            var recentFiles = Directory.GetFiles(convDir, "*.md")
                .OrderByDescending(f => f)
                .Take(3);
            foreach (var file in recentFiles)
            {
                try
                {
                    var content = ReadFirstLines(file, 200);
                    index.RecentConversations.Add(new MemoryEntry
                    {
                        Category = "conversation",
                        Content = content,
                        Source = file,
                        CreatedAt = File.GetLastWriteTime(file)
                    });
                }
                catch { /* 跳过损坏文件 */ }
            }
        }

        return index;
    }

    /// <summary>
    /// 解析 Markdown 中 "- xxx" 格式的列表行为 MemoryEntry。
    /// </summary>
    private List<MemoryEntry> ParseBulletFile(string filePath, string category)
    {
        var entries = new List<MemoryEntry>();
        try
        {
            var lines = File.ReadAllLines(filePath);
            var lastWrite = File.GetLastWriteTime(filePath);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("- ") && trimmed.Length > 2)
                {
                    entries.Add(new MemoryEntry
                    {
                        Category = category,
                        Content = trimmed[2..],
                        Source = filePath,
                        CreatedAt = lastWrite,
                        UpdatedAt = lastWrite
                    });
                }
            }
        }
        catch { /* 跳过损坏文件 */ }
        return entries;
    }

    /// <summary>
    /// 读取文件前 N 行。
    /// </summary>
    private string ReadFirstLines(string path, int maxLines)
    {
        var lines = File.ReadAllLines(path);
        return string.Join("\n", lines.Take(maxLines));
    }

    /// <summary>
    /// 首次启动时创建初始文件。
    /// </summary>
    private void CreateDefaultFiles()
    {
        Directory.CreateDirectory(Path.Combine(_memoryDir, "conversation"));
        Directory.CreateDirectory(Path.Combine(_memoryDir, "compressed"));
        File.WriteAllText(Path.Combine(_memoryDir, "INDEX.md"),
            "# 千千记忆索引\n\n> 自动生成，每次整理时更新\n\n## 最近对话\n\n（暂无）\n");
        File.WriteAllText(Path.Combine(_memoryDir, "preferences.md"),
            "# 用户偏好\n\n- 回复用中文，简洁\n- 称呼用户为\"你\"\n");
        File.WriteAllText(Path.Combine(_memoryDir, "knowledge.md"),
            "# 长期知识\n\n- 千千是 WPF .NET 8 桌面宠物应用\n- 项目位于 D:\\dev\\claude-pet\n");
        File.WriteAllText(Path.Combine(_memoryDir, "tasks.md"),
            "# 未完成任务\n\n（暂无）\n");
    }
}
```

- [ ] **Step 2: 编译验证**

```bash
dotnet build claude-pet.sln
```

---

### Task 3: 创建 MemoryWriter — 文件写入 + 去重

**Files:**
- Create: `src/ClaudePet/Memory/MemoryWriter.cs`

- [ ] **Step 1: 创建 MemoryWriter.cs**

```csharp
using System.Text;

namespace ClaudePet.Memory;

/// <summary>
/// 写入/更新记忆文件，带去重。
/// </summary>
public class MemoryWriter
{
    private readonly string _memoryDir;

    public MemoryWriter(string memoryDir)
    {
        _memoryDir = memoryDir;
        Directory.CreateDirectory(_memoryDir);
        Directory.CreateDirectory(Path.Combine(_memoryDir, "conversation"));
        Directory.CreateDirectory(Path.Combine(_memoryDir, "compressed"));
    }

    /// <summary>
    /// 追加对话摘要到当天文件。
    /// </summary>
    public void WriteConversation(string date, string summary)
    {
        var file = Path.Combine(_memoryDir, "conversation", $"{date}.md");
        var header = File.Exists(file) ? "" : $"# 对话记录 — {date}\n\n";
        var entry = $"- **{DateTime.Now:HH:mm}** {summary.Trim()}\n";
        File.AppendAllText(file, header + entry, Encoding.UTF8);
    }

    /// <summary>
    /// 合并更新偏好文件（去重）。
    /// </summary>
    public void UpdatePreferences(List<string> newPrefs)
    {
        MergeBulletFile(Path.Combine(_memoryDir, "preferences.md"),
            "# 用户偏好\n", newPrefs);
    }

    /// <summary>
    /// 合并更新任务文件。
    /// </summary>
    public void UpdateTasks(List<string> newTasks)
    {
        MergeBulletFile(Path.Combine(_memoryDir, "tasks.md"),
            "# 未完成任务\n", newTasks);
    }

    /// <summary>
    /// 合并更新知识文件。
    /// </summary>
    public void UpdateKnowledge(List<string> newKnowledge)
    {
        MergeBulletFile(Path.Combine(_memoryDir, "knowledge.md"),
            "# 长期知识\n", newKnowledge);
    }

    /// <summary>
    /// 将 >7 天的 conversation 文件压缩到 compressed/ 文件夹。
    /// </summary>
    public void ArchiveOldConversations(int olderThanDays = 7)
    {
        var convDir = Path.Combine(_memoryDir, "conversation");
        if (!Directory.Exists(convDir)) return;

        var cutoff = DateTime.Now.AddDays(-olderThanDays);
        var oldFiles = Directory.GetFiles(convDir, "*.md")
            .Where(f => File.GetLastWriteTime(f) < cutoff)
            .ToList();

        if (oldFiles.Count == 0) return;

        // 生成周汇总
        var weekLabel = $"week-{cutoff:yyyy-MM}";
        var compressedFile = Path.Combine(_memoryDir, "compressed", $"summary-{weekLabel}.md");
        var sb = new StringBuilder();
        sb.AppendLine($"# 压缩摘要 — {weekLabel}");
        sb.AppendLine();

        foreach (var file in oldFiles.OrderBy(f => f))
        {
            try
            {
                var content = File.ReadAllText(file);
                sb.AppendLine($"## {Path.GetFileNameWithoutExtension(file)}");
                sb.AppendLine(content);
                sb.AppendLine();
                File.Delete(file);
            }
            catch { }
        }

        File.WriteAllText(compressedFile, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// 写入 INDEX.md 时间线。
    /// </summary>
    public void RebuildIndex(List<MemoryEntry> conversations)
    {
        var file = Path.Combine(_memoryDir, "INDEX.md");
        var sb = new StringBuilder();
        sb.AppendLine("# 千千记忆索引");
        sb.AppendLine();
        sb.AppendLine($"> 最后整理: {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();
        sb.AppendLine("## 最近对话");
        sb.AppendLine();

        foreach (var c in conversations.Take(10))
        {
            sb.AppendLine($"- {c.CreatedAt:MM-dd HH:mm} {c.Content}");
        }

        File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// 合并去重：已有文件中不重复添加。
    /// </summary>
    private void MergeBulletFile(string path, string header, List<string> newItems)
    {
        var existing = new HashSet<string>();
        if (File.Exists(path))
        {
            var lines = File.ReadAllLines(path);
            foreach (var line in lines)
            {
                var t = line.Trim();
                if (t.StartsWith("- ") && t.Length > 2)
                    existing.Add(t[2..]);
            }
        }

        var toAdd = newItems
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Where(item => !existing.Contains(item.Trim()))
            .Select(item => $"- {item.Trim()}")
            .ToList();

        if (toAdd.Count == 0) return;

        if (!File.Exists(path))
            File.WriteAllText(path, header, Encoding.UTF8);

        File.AppendAllLines(path, toAdd, Encoding.UTF8);
    }
}
```

- [ ] **Step 2: 编译验证**

```bash
dotnet build claude-pet.sln
```

---

### Task 4: 创建 ContextBuilder — prompt 前缀拼接

**Files:**
- Create: `src/ClaudePet/Memory/ContextBuilder.cs`

- [ ] **Step 1: 创建 ContextBuilder.cs**

```csharp
using System.Text;

namespace ClaudePet.Memory;

/// <summary>
/// 对话前从 MemoryIndex 提取相关记忆，拼接 prompt 前缀。
/// 总前缀控制在 ~500 tokens（约 1000 中文字符）。
/// </summary>
public class ContextBuilder
{
    // 每个来源的字符预算
    private const int TasksBudget = 200;
    private const int PrefsBudget = 200;
    private const int KnowledgeBudget = 300;
    private const int ChatBudget = 300;

    /// <summary>
    /// 构建记忆前缀字符串。
    /// </summary>
    public string BuildPrefix(MemoryIndex index)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[千千记忆]");

        // 1. 未完成任务（最高优先级）
        var activeTasks = index.Tasks
            .Where(t => !t.Content.Contains("（已完成）"))
            .Take(5).ToList();
        if (activeTasks.Count > 0)
        {
            sb.AppendLine("## 未完成任务");
            foreach (var t in activeTasks)
                sb.AppendLine($"- {Truncate(t.Content, TasksBudget / activeTasks.Count)}");
        }

        // 2. 用户偏好
        var prefs = index.Preferences.Take(5).ToList();
        if (prefs.Count > 0)
        {
            sb.AppendLine("## 用户偏好");
            foreach (var p in prefs)
                sb.AppendLine($"- {Truncate(p.Content, PrefsBudget / prefs.Count)}");
        }

        // 3. 长期知识（取最近更新的 3 条）
        var knowledge = index.Knowledge.OrderByDescending(k => k.UpdatedAt).Take(3).ToList();
        if (knowledge.Count > 0)
        {
            sb.AppendLine("## 项目知识");
            foreach (var k in knowledge)
                sb.AppendLine($"- {Truncate(k.Content, KnowledgeBudget / knowledge.Count)}");
        }

        // 4. 最近对话摘要
        var chats = index.RecentConversations.Take(3).ToList();
        if (chats.Count > 0)
        {
            sb.AppendLine("## 最近对话");
            foreach (var c in chats)
            {
                var summary = ExtractSummary(c.Content);
                sb.AppendLine($"- {Truncate(summary, ChatBudget / chats.Count)}");
            }
        }

        sb.AppendLine("---");

        var result = sb.ToString();
        // 如果总长度超过 2000 字符，裁剪优先级低的
        if (result.Length > 2000)
        {
            result = TrimToBudget(sb, index);
        }

        return result;
    }

    private string Truncate(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLen ? text : text[..(maxLen - 3)] + "...";
    }

    /// <summary>
    /// 从对话文件中提取前几行作为摘要。
    /// </summary>
    private string ExtractSummary(string content)
    {
        if (string.IsNullOrEmpty(content)) return "";
        var lines = content.Split('\n');
        // 取第一个非空非标题行
        foreach (var line in lines)
        {
            var t = line.Trim();
            if (!string.IsNullOrEmpty(t) && !t.StartsWith("#"))
                return t;
        }
        return lines.FirstOrDefault() ?? "";
    }

    /// <summary>
    /// 超预算时按优先级裁剪。
    /// </summary>
    private string TrimToBudget(StringBuilder sb, MemoryIndex index)
    {
        // 简化处理：只用任务 + 偏好 + 知识，砍掉对话
        var minimal = new StringBuilder();
        minimal.AppendLine("[千千记忆]");

        var tasks = index.Tasks.Where(t => !t.Content.Contains("（已完成）")).Take(3).ToList();
        if (tasks.Count > 0)
        {
            minimal.AppendLine("## 未完成任务");
            foreach (var t in tasks) minimal.AppendLine($"- {t.Content}");
        }

        var prefs = index.Preferences.Take(3).ToList();
        if (prefs.Count > 0)
        {
            minimal.AppendLine("## 用户偏好");
            foreach (var p in prefs) minimal.AppendLine($"- {p.Content}");
        }

        minimal.AppendLine("---");
        return minimal.ToString();
    }
}
```

- [ ] **Step 2: 编译验证**

```bash
dotnet build claude-pet.sln
```

---

### Task 5: 创建 Compressor — 上下文压缩

**Files:**
- Create: `src/ClaudePet/Memory/Compressor.cs`

- [ ] **Step 1: 创建 Compressor.cs**

```csharp
using ClaudePet.Bridge;

namespace ClaudePet.Memory;

/// <summary>
/// 上下文压缩器。当 context% > 70% 时触发，调 Claude 生成摘要。
/// </summary>
public class Compressor
{
    private readonly ClaudeBridge _bridge;
    private readonly MemoryWriter _writer;
    private DateTime _lastCompressAt = DateTime.MinValue;
    private bool _isCompressing;

    public bool IsCompressing => _isCompressing;

    public Compressor(ClaudeBridge bridge, MemoryWriter writer)
    {
        _bridge = bridge;
        _writer = writer;
    }

    /// <summary>
    /// 检查是否需要压缩，需要则执行。
    /// </summary>
    /// <returns>是否执行了压缩</returns>
    public async Task<bool> TryCompress(double contextPercent)
    {
        // 阈值检查
        if (contextPercent < 70) return false;

        // 防抖：至少间隔 5 分钟
        if ((DateTime.Now - _lastCompressAt).TotalMinutes < 5) return false;

        // 正在压缩中
        if (_isCompressing) return false;

        _isCompressing = true;
        try
        {
            await DoCompress();
            _lastCompressAt = DateTime.Now;
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            _isCompressing = false;
        }
    }

    private async Task DoCompress()
    {
        var prompt = "请将当前对话历史压缩成一段摘要。" +
            "保留所有任务、决策、关键信息，丢弃冗余细节。" +
            "用中文输出，不超过 300 字。只输出摘要文字，不要其他内容。";

        var result = await _bridge.SendPrompt(prompt);
        var summary = result.Trim();

        // 去掉可能的 markdown 标记
        if (summary.StartsWith("```")) summary = summary.Split("\n").Skip(1).FirstOrDefault() ?? summary;
        summary = summary.Replace("```", "").Trim();

        if (!string.IsNullOrWhiteSpace(summary) && summary.Length > 10)
        {
            var date = DateTime.Now.ToString("yyyy-MM-dd");
            _writer.WriteConversation(date, summary);

            // 归档旧对话
            _writer.ArchiveOldConversations(7);
        }
    }
}
```

- [ ] **Step 2: 编译验证**

```bash
dotnet build claude-pet.sln
```

---

### Task 6: 创建 IdleOrganizer — 空闲记忆整理

**Files:**
- Create: `src/ClaudePet/Memory/IdleOrganizer.cs`

- [ ] **Step 1: 创建 IdleOrganizer.cs**

```csharp
using ClaudePet.Bridge;

namespace ClaudePet.Memory;

/// <summary>
/// 空闲记忆整理调度器。千千空闲时随机选 1-2 项整理任务执行。
/// </summary>
public class IdleOrganizer
{
    private readonly ClaudeBridge _bridge;
    private readonly MemoryWriter _writer;
    private readonly MemoryLoader _loader;
    private readonly string _memoryDir;
    private DateTime _lastOrganizeAt = DateTime.MinValue;
    private bool _isOrganizing;
    private readonly Random _rng = new();

    public bool IsOrganizing => _isOrganizing;

    /// <summary>整理开始/结束事件（供 UI 播放思考动画）</summary>
    public event Action<bool>? OrganizeStateChanged;

    public IdleOrganizer(ClaudeBridge bridge, MemoryWriter writer, MemoryLoader loader, string memoryDir)
    {
        _bridge = bridge;
        _writer = writer;
        _loader = loader;
        _memoryDir = memoryDir;
    }

    /// <summary>
    /// 尝试触发一次整理。不适合整理时返回 false。
    /// </summary>
    public async Task<bool> TryOrganize(DateTime lastInteraction)
    {
        // 至少空闲 5 分钟
        if ((DateTime.Now - lastInteraction).TotalMinutes < 5) return false;
        // 距上次整理 >30 分钟
        if ((DateTime.Now - _lastOrganizeAt).TotalMinutes < 30) return false;
        // 正在整理中
        if (_isOrganizing) return false;

        _isOrganizing = true;
        OrganizeStateChanged?.Invoke(true);

        try
        {
            await DoOrganize();
            _lastOrganizeAt = DateTime.Now;
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            _isOrganizing = false;
            OrganizeStateChanged?.Invoke(false);
        }
    }

    private async Task DoOrganize()
    {
        // 随机选 1-2 项任务
        var tasks = new List<Func<Task>>
        {
            ExtractPreferences,
            UpdateTasksFromChat,
            ExtractKnowledge,
            RebuildIndex,
            ArchiveOld
        };

        // 随机挑 1-2 个
        var selected = tasks.OrderBy(_ => _rng.Next()).Take(2).ToList();
        foreach (var task in selected)
        {
            await task();
        }
    }

    private async Task ExtractPreferences()
    {
        var prompt = "请检查 memory/conversation/ 最近对话，" +
            "提取用户新表达的习惯、偏好、常用指令，" +
            "以 Markdown 列表格式输出（每行 '- xxx'）。" +
            "不要重复已有内容。没有新偏好就输出 '无'。";

        var result = await _bridge.SendPrompt(prompt);
        var items = ParseResultItems(result);
        if (items.Count > 0)
            _writer.UpdatePreferences(items);
    }

    private async Task UpdateTasksFromChat()
    {
        var prompt = "请检查 memory/conversation/ 最近对话，" +
            "列出所有未完成的任务和新出现的待办事项，" +
            "以 Markdown 列表格式输出（每行 '- xxx'）。" +
            "已完成的任务标注 '（已完成）'。没有新任务就输出 '无'。";

        var result = await _bridge.SendPrompt(prompt);
        var items = ParseResultItems(result);
        if (items.Count > 0)
            _writer.UpdateTasks(items);
    }

    private async Task ExtractKnowledge()
    {
        var prompt = "请检查 memory/conversation/ 最近对话，" +
            "提取关于项目架构、技术决策、关键信息，" +
            "以 Markdown 列表格式输出（每行 '- xxx'）。" +
            "不要重复已有内容。没有新知识就输出 '无'。";

        var result = await _bridge.SendPrompt(prompt);
        var items = ParseResultItems(result);
        if (items.Count > 0)
            _writer.UpdateKnowledge(items);
    }

    private Task RebuildIndex()
    {
        var index = _loader.Load();
        _writer.RebuildIndex(index.RecentConversations);
        return Task.CompletedTask;
    }

    private Task ArchiveOld()
    {
        _writer.ArchiveOldConversations(7);
        return Task.CompletedTask;
    }

    private List<string> ParseResultItems(string result)
    {
        var items = new List<string>();
        if (string.IsNullOrWhiteSpace(result)) return items;

        foreach (var line in result.Split('\n'))
        {
            var t = line.Trim();
            if (t.StartsWith("- ") && t.Length > 2 && t != "- 无")
                items.Add(t[2..]);
        }
        return items;
    }
}
```

- [ ] **Step 2: 编译验证**

```bash
dotnet build claude-pet.sln
```

---

### Task 7: 创建 MemoryEngine — 总协调器

**Files:**
- Create: `src/ClaudePet/Memory/MemoryEngine.cs`

- [ ] **Step 1: 创建 MemoryEngine.cs**

```csharp
using ClaudePet.Bridge;

namespace ClaudePet.Memory;

/// <summary>
/// 记忆系统总协调器。对外暴露 SendWithMemory() 替代直接调 ClaudeBridge。
/// 持有所有子组件，管理压缩和整理流程。
/// </summary>
public class MemoryEngine : IDisposable
{
    private readonly ClaudeBridge _bridge;
    private readonly MemoryLoader _loader;
    private readonly MemoryWriter _writer;
    private readonly ContextBuilder _contextBuilder;
    private readonly Compressor _compressor;
    private readonly IdleOrganizer _organizer;
    private MemoryIndex _index;
    private bool _disposed;

    /// <summary>当记忆被更新时触发</summary>
    public event Action? MemoryUpdated;
    /// <summary>整理状态变化（供 UI 切换动画）</summary>
    public event Action<bool>? OrganizeStateChanged;

    public MemoryEngine(ClaudeBridge bridge, string memoryDir)
    {
        _bridge = bridge;
        _loader = new MemoryLoader(memoryDir);
        _writer = new MemoryWriter(memoryDir);
        _contextBuilder = new ContextBuilder();
        _compressor = new Compressor(bridge, _writer);
        _organizer = new IdleOrganizer(bridge, _writer, _loader, memoryDir);

        _organizer.OrganizeStateChanged += (state) => OrganizeStateChanged?.Invoke(state);

        // 启动时加载记忆
        _index = _loader.Load();

        // 监听 Claude 状态（context% 变化时尝试压缩）
        _bridge.StateUpdated += OnStateUpdated;
    }

    /// <summary>
    /// 带记忆前缀发送消息给 Claude。
    /// </summary>
    public async Task<string> SendWithMemory(string userMessage)
    {
        // 重新加载索引（可能被整理线程更新了文件）
        _index = _loader.Load();

        // 拼接记忆前缀
        var prefix = _contextBuilder.BuildPrefix(_index);
        var fullPrompt = prefix + "\n" + userMessage;

        // 发给 Claude
        var reply = await _bridge.SendPrompt(fullPrompt);

        // 异步保存对话摘要（不阻塞回复显示）
        _ = Task.Run(() => SaveConversationSummary(userMessage, reply));

        return reply;
    }

    /// <summary>
    /// 保存本轮对话摘要。
    /// </summary>
    private async Task SaveConversationSummary(string userMsg, string reply)
    {
        try
        {
            var shortReply = reply.Length > 200 ? reply[..200] + "..." : reply;
            var summary = $"用户: {userMsg[..Math.Min(userMsg.Length, 100)]} → 千千: {shortReply}";
            var date = DateTime.Now.ToString("yyyy-MM-dd");
            _writer.WriteConversation(date, summary);
            MemoryUpdated?.Invoke();
        }
        catch { }
    }

    /// <summary>
    /// Claude 状态变化时检查是否需要压缩或整理。
    /// </summary>
    private async void OnStateUpdated(Models.StateSnapshot state)
    {
        // 空闲时尝试整理
        if (state.Status == "idle")
        {
            var organized = await _organizer.TryOrganize(DateTime.Now.AddMinutes(-10));
            if (organized) MemoryUpdated?.Invoke();
        }

        // 上下文超阈值时尝试压缩
        var compressed = await _compressor.TryCompress(state.ContextPercent);
        if (compressed) MemoryUpdated?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _bridge.StateUpdated -= OnStateUpdated;
    }
}
```

- [ ] **Step 2: 编译验证**

```bash
dotnet build claude-pet.sln
```

---

### Task 8: 修改 App.xaml.cs — 创建并注入 MemoryEngine

**Files:**
- Modify: `src/ClaudePet/App.xaml.cs`

- [ ] **Step 1: 在 App.xaml.cs 中创建 MemoryEngine**

找到创建 ClaudeBridge 之后的代码段，修改为：

```csharp
using ClaudePet.Memory;  // 在文件顶部 using 区添加

// 在 Application_Startup 中，创建 _bridge 之后：
var memoryDir = Path.Combine(baseDir, "memory");
var memoryEngine = new MemoryEngine(_bridge, memoryDir);

// 原来传 _bridge 给 PetWindow 的地方，额外传 memoryEngine
_petWindow = new PetWindow(config, _bridge, _voiceEngine, ttsEngine, memoryEngine);
```

- [ ] **Step 2: 完整修改后的 Application_Startup 关键部分**

定位到 `_bridge = new ClaudeBridge(...)` 之后，`_petWindow = new PetWindow(...)` 之前，插入 MemoryEngine 创建代码。修改 PetWindow 构造调用。

- [ ] **Step 3: 编译验证**

```bash
dotnet build claude-pet.sln
```

---

### Task 9: 修改 PetWindow.xaml.cs — 使用 SendWithMemory

**Files:**
- Modify: `src/ClaudePet/UI/PetWindow.xaml.cs`

- [ ] **Step 1: PetWindow 构造函数增加 memoryEngine 参数**

```csharp
// 在文件顶部加 using
using ClaudePet.Memory;

// 构造函数改为：
private readonly MemoryEngine _memoryEngine;

public PetWindow(ConfigStore config, ClaudeBridge bridge,
    VoiceEngine voiceEngine, ITTSEngine ttsEngine, MemoryEngine memoryEngine)
{
    // ... 现有代码 ...
    _memoryEngine = memoryEngine;
    InitializeComponent();
}
```

- [ ] **Step 2: 聊天发送改用 SendWithMemory**

在 InitWindow 中，找到 `ChatBubble.MessageSent += async (msg) =>` 这段，把 `_bridge.SendPrompt(msg)` 改为 `_memoryEngine.SendWithMemory(msg)`。

同时修改语音 TranscriptionReady 中的 `_bridge.SendPrompt(text)` 为 `_memoryEngine.SendWithMemory(text)`。

- [ ] **Step 3: 空闲时播放整理动画**

在 InitWindow 中订阅 `_memoryEngine.OrganizeStateChanged`：

```csharp
_memoryEngine.OrganizeStateChanged += (isOrganizing) =>
{
    Dispatcher.Invoke(() =>
    {
        if (isOrganizing)
            _animationPlayer?.Play(AnimationState.Think);
        else if (!_isSleeping)
            _animationPlayer?.Play(AnimationState.Idle);
    });
};
```

- [ ] **Step 4: 编译验证**

```bash
dotnet build claude-pet.sln
```

---

### Task 10: 最终验证

- [ ] **Step 1: 完整编译**

```bash
dotnet build claude-pet.sln
```
预期：0 errors

- [ ] **Step 2: 启动验证**

启动千千后检查：
- `D:\dev\claude-pet\memory\` 目录是否自动创建
- 初始文件（preferences.md, knowledge.md, tasks.md, INDEX.md）是否生成
- 发送一条消息后，`conversation/` 下是否有当天文件
- 重启千千后，头上气泡是否显示之前的任务/偏好

- [ ] **Step 3: 压缩验证**

多次对话后检查 context% 是否触发压缩，`compressed/` 目录是否有文件生成。

---

## 实现总结

| 任务 | 文件 | 关键点 |
|------|------|--------|
| 1 | MemoryModels.cs | 纯数据结构，无依赖 |
| 2 | MemoryLoader.cs | 文件→索引，解析 Markdown |
| 3 | MemoryWriter.cs | 索引→文件，去重合并 |
| 4 | ContextBuilder.cs | 索引→prompt 前缀，~500 token 预算 |
| 5 | Compressor.cs | context%>70 → 调 Claude 压缩 |
| 6 | IdleOrganizer.cs | 空闲 >5 分钟 → 随机 1-2 项整理 |
| 7 | MemoryEngine.cs | 总协调器，暴露 SendWithMemory |
| 8 | App.xaml.cs | 创建 MemoryEngine，注入 PetWindow |
| 9 | PetWindow.xaml.cs | SendPrompt → SendWithMemory |
| 10 | 最终验证 | 编译 + 功能检查 |
