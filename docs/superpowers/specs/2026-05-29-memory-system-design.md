# 千千记忆系统 — 设计文档

> 让千千拥有持久记忆：重启不丢失、自动压缩、空闲整理

---

## 1. 概述

### 目标
- 重启电脑/重启千千后，之前的**对话、工作任务、用户偏好**全部保留
- 上下文过长时**自动压缩**，不丢失关键信息
- 千千空闲时**主动整理记忆**（去重、提取偏好、更新任务）

### 核心原则
- **纯文件存储** — Markdown 文件，透明可读，Git 友好
- **自动管理** — 完全不用用户操心，千千自己判断何时记、何时压缩、何时整理
- **Prompt 注入 + 文件读取混合** — 短记忆注入 prompt，长记忆存文件让 Claude 按需读取

---

## 2. 架构

```
                        ┌─────────────────────────┐
                        │     MemoryEngine         │
                        │  ┌───────────────────┐   │
                        │  │  MemoryLoader     │   │  启动时加载 memory/*.md
                        │  │  MemoryWriter     │   │  写入/更新记忆文件
                        │  │  ContextBuilder   │   │  对话前拼 prompt 前缀
                        │  │  Compressor       │   │  上下文 >70% 时压缩
                        │  │  IdleOrganizer    │   │  空闲时整理
                        │  └───────────────────┘   │
                        └──────────┬──────────────┘
                                   │
    App.xaml.cs ──→ ClaudeBridge ──→ ProcessManager ──→ claude -p
                       │                                  ↑
                       │     SendPrompt(msg) 变成         │
                       │     SendPrompt(contextPrefix + msg)
```

### 文件结构

```
D:\dev\claude-pet\memory\
├── INDEX.md              ← 记忆索引：时间线 + 分类
├── preferences.md        ← 用户偏好、习惯、常用指令
├── knowledge.md          ← 长期知识：项目架构、关键决策
├── tasks.md              ← 未完成任务清单
├── conversation\         ← 对话记录
│   ├── 2026-05-29.md     ← 每天一个文件，对话摘要
│   └── 2026-05-30.md
└── compressed\           ← 被压缩的旧记忆
    └── 2026-05-week-1.md ← 按周汇总摘要
```

---

## 3. 组件设计

### 3.1 MemoryEngine（总协调器）

**生命周期：** 在 `App.xaml.cs` 启动时创建，和 `ClaudeBridge` 同级。

**职责：**
- 持有所有子组件（Loader / Writer / ContextBuilder / Compressor / IdleOrganizer）
- 对外暴露 `Task<string> SendWithMemory(string userMessage)` — PetWindow 调这个方法替代直接调 ClaudeBridge
- 管理空闲检测 → 触发压缩/整理
- 公开事件 `MemoryUpdated` — 通知 UI 记忆状态变化

**与 ClaudeBridge 的关系：**
- 持有 ClaudeBridge 引用，但不修改其内部逻辑
- ClaudeBridge 继续提供 `SendPrompt(string)` + `StateUpdated` 事件
- MemoryEngine 在 `SendPrompt` 前拼记忆前缀，拦截消息

### 3.2 MemoryLoader

**启动时：** 读取 `memory/` 下所有 `.md` 文件，构建内存中的索引结构：

```csharp
class MemoryIndex
{
    List<MemoryEntry> Tasks;         // 未完成任务
    List<MemoryEntry> Preferences;   // 用户偏好
    List<MemoryEntry> Knowledge;     // 长期知识
    List<MemoryEntry> RecentChats;   // 最近 3 天对话摘要
}
```

**加载策略：** 按文件修改时间排序，最近的文件优先。单个文件超过 50KB 时只读前 200 行。

### 3.3 MemoryWriter

**职责：** 写入/更新记忆文件

- `WriteConversation(date, summary)` — 追加到 `conversation/YYYY-MM-DD.md`
- `UpdatePreferences(entries)` — 合并更新 `preferences.md`
- `UpdateTasks(entries)` — 合并更新 `tasks.md`
- `UpdateKnowledge(entries)` — 合并更新 `knowledge.md`
- `CompressOldMemories()` — 将 >7 天的对话移入 `compressed/`

**写入策略：** 对比去重 — 新内容与已有内容相似度 >80% 时跳过写入。

### 3.4 ContextBuilder

**每次对话前** 从 MemoryIndex 提取相关内容，拼成 prompt 前缀：

| 优先级 | 来源 | token 预算 |
|--------|------|-----------|
| 1 | `tasks.md` 未完成任务 | ~100 |
| 2 | `preferences.md` 最新 5 条 | ~100 |
| 3 | `knowledge.md` 匹配话题 | ~150 |
| 4 | `INDEX.md` 最近 3 天摘要 | ~150 |

**总前缀控制在 ~500 tokens。** 超限时按优先级从低到高截断。

**拼接格式：**
```
[千千记忆]
## 未完成任务
- xxx

## 用户偏好
- xxx

## 最近对话
- xxx
---
[用户原始消息]
```

### 3.5 Compressor

**触发条件：**
- TranscriptWatcher 检测 `ContextPercent > 70%`（约 140K / 200K tokens）
- 距上次压缩 >5 分钟（防抖）
- 千千当前不在对话中

**压缩流程：**
1. 千千自主发一次 `claude -p`，prompt 为：
   ```
   请将以下对话历史压缩成摘要，保留所有任务、决策、关键信息。
   丢弃冗余细节。用中文输出，不超过 300 字。
   ```
2. Claude 返回摘要 → MemoryWriter 写入当天 `conversation/` 文件
3. 被压缩的原对话移入 `compressed/` 文件夹
4. 发起新的 `--continue` 会话重置上下文
5. 下次对话时 ContextBuilder 从压缩摘要中恢复

**压缩产物示例：**
```markdown
# 压缩摘要 — 2026年第4周
## 05-29
- 实现了千千桌面宠物骨架
- 修复 DPI 缩放窗口定位
- 改名千千，添加语音识别
## 关键决策
- 动画用 WPF 程序化椭圆
- 语音热键用 Win32 GetAsyncKeyState
```

### 3.6 IdleOrganizer

**触发条件：**
- 千千启动后 2 分钟无工作 **或** 工作完成后空闲 >5 分钟
- 距离上次整理 >30 分钟
- 千千不在睡觉状态

**执行时千千播放"思考"动画，完成后恢复"就绪"。**

**整理任务（每次随机选 1-2 项）：**

| 任务 | 说明 | 频率 |
|------|------|------|
| 去重合并 | 检查最近 3 天 conversation 文件，合并重复信息 | 每天一次 |
| 偏好提取 | 从对话中提取新偏好/习惯，更新 preferences.md | 每次空闲 |
| 任务更新 | 对比 tasks.md 和对话，标记完成/新增任务 | 每次空闲 |
| 知识固化 | 将架构、决策信息写入 knowledge.md | 每天一次 |
| 索引重建 | 重新生成 INDEX.md 时间线 | 每天一次 |
| 过期清理 | 压缩 >7 天对话 → compressed/ | 每周一次 |

**执行方式：** 千千发整理指令给 Claude（独立进程，不影响当前会话上下文）

---

## 4. 文件变更

```
新增:
  src/ClaudePet/Memory/
    ├── MemoryEngine.cs        # 总协调器
    ├── MemoryLoader.cs        # 文件加载 + 索引构建
    ├── MemoryWriter.cs        # 文件写入 + 去重
    ├── ContextBuilder.cs      # prompt 前缀拼接
    ├── Compressor.cs          # 上下文压缩
    ├── IdleOrganizer.cs       # 空闲整理
    └── MemoryModels.cs        # MemoryEntry / MemoryIndex 数据结构

修改:
  src/ClaudePet/App.xaml.cs           # 创建 MemoryEngine，注入 ClaudeBridge
  src/ClaudePet/UI/PetWindow.xaml.cs  # 改用 memoryEngine.SendWithMemory()
  src/ClaudePet/Models/StateSnapshot.cs # 加 memory 相关字段（可选）
```

---

## 5. 边界条件 & 异常处理

| 场景 | 处理 |
|------|------|
| `memory/` 文件夹不存在（首次启动） | 自动创建空目录 + 空文件 |
| 记忆文件被手动损坏 | 跳过损坏文件，写 warning 日志 |
| 压缩时 Claude 返回乱码 | 重试一次，仍失败则跳过本次压缩 |
| 空闲整理时用户突然发消息 | 中断整理，优先响应用户 |
| 磁盘空间不足 | 不写入新记忆，提示用户清理 |
| memory/ 文件被外部编辑 | 下次加载时自动识别，不做冲突合并（以文件内容为准） |

---

## 6. 验证检查单

- [ ] 首次启动自动创建 `memory/` 目录和初始文件
- [ ] 对话后 conversation 文件自动更新
- [ ] 重启千千后，ContextBuilder 能从文件恢复上次对话摘要
- [ ] 上下文 >70% 时自动触发压缩
- [ ] 压缩后旧对话移入 compressed/，摘要保留
- [ ] 空闲 >5 分钟自动触发整理（偏好提取、任务更新）
- [ ] 整理期间用户发消息，优先响应用户
- [ ] `memory/` 下所有文件为合法 Markdown，可手动编辑
