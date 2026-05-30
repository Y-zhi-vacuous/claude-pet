# 千千桌宠开发总结

> 2026-05-29 ~ 2026-05-30

---

## 一、项目概述

千千是一只 Windows 桌面像素小狗，作为 Claude Code 的桌面交互界面。用户通过聊天窗与 Claude Code 对话，小狗展示 Claude 的工作状态。技术栈：.NET 8 + WPF + C# 12。

---

## 二、开发历程

### 阶段 1：骨架搭建（已有基础）

**已有代码：**
- 透明 WPF 窗口 + 程序化动画（Ellipse 拼的小狗）
- ClaudeBridge（ProcessManager + TranscriptWatcher）
- 语音引擎（KeyboardWakeDetector + VAD + Edge TTS）
- 聊天气泡、系统托盘、配置管理

### 阶段 2：功能补全

**实现的功能：**
- 11 个动画状态全补全（Walk↑↓←→、Eat 等）
- 空闲睡眠计时器（5 分钟自动 SitSleep）
- 双击玩耍、拖拽走动动画
- 设置面板（SettingsWindow.xaml）
- 托盘菜单接通

### 阶段 3：像素风改造

**问题：** 程序化椭圆拼的小狗太丑。

**解决：** 
- 重写 SpriteGenerator，96×96 像素风逐像素画比熊
- SpriteSheetPlayer 从 Ellipse 改为 Image 帧动画
- 后改用用户提供的 `Image/千千.avif` → 转 PNG → 单图 + 程序化动效（缩放/旋转/位移）

**结论：** 最终使用用户图片 + 程序化动效，放弃像素精灵生成。

### 阶段 4：记忆系统（后删除）

**实现：**
- MemoryEngine 总协调器 + ContextBuilder / Loader / Writer / Compressor / IdleOrganizer
- SOUL.MD 人设系统
- 对话自动记忆、上下文压缩、空闲整理

**最终删除原因：** 记忆系统导致千千身份混乱，详见下方"核心问题"。

### 阶段 5：大简化

删除全部记忆系统和 SOUL.MD，千千回归本质——Claude Code 的桌面聊天窗口。

---

## 三、核心问题及根因

### 问题 1：窗口不显示

**现象：** 启动后桌面看不到小狗。

**根因：** DPI 缩放（200%）。WinForms `Screen.PrimaryScreen.WorkingArea` 返回物理像素（1536×912），但 WPF 的 `Window.Left/Top` 使用设备无关坐标。窗口被定位到屏幕外（Left=2852，屏幕只有 1536）。

**修复：** 改用 WPF 的 `SystemParameters.WorkArea`（统一坐标系）。

```csharp
// 修复前
var screen = System.Windows.Forms.Screen.PrimaryScreen;
Left = screen.WorkingArea.Right - Width - 20;  // ← 坐标不一致

// 修复后
var workArea = System.Windows.SystemParameters.WorkArea;
Left = workArea.Right - Width - 20;  // ← WPF 坐标，与 Window.Left 一致
```

### 问题 2：聊天窗闪退

**现象：** 点击小狗弹出聊天窗，立马消失。

**根因：** `Popup.StaysOpen="False"` —— 用户点输入框时焦点变化，Popup 自动关闭。

**修复：** 改为 `StaysOpen="True"`，加关闭按钮手动关闭。

### 问题 3：Claude 找不到

**现象：** 聊天发消息报错"找不到指定文件"。

**根因：** 桌面双击启动的进程 PATH 不含 npm 全局目录（`%APPDATA%/npm`），`claude` 命令找不到。

**修复：** ProcessManager 启动时搜索 claude 的完整路径：
```csharp
public static string FindClaudePath()
{
    // 搜索 %APPDATA%/npm/claude.cmd、PATH 中的目录等
    // 找到就用完整路径，找不到 fallback "claude"
}
```

### 问题 4：语音不工作

**现象 4a：Ctrl+Shift+W 不响应**

**根因：** WPF 的 `Keyboard.IsKeyDown()` 只在窗口有焦点时才能检测按键。

**修复：** 改用 Win32 `GetAsyncKeyState` API（全局按键检测，不需要焦点）。

**现象 4b：语音识别不可用**

**根因：** 用了 `SpeechRecognitionEngine`（进程内引擎），需要手动喂音频数据，PCM→WAV 格式转换复杂，可靠性和精度差。

**修复：** 改用 `SpeechRecognizer`（系统共享识别器），直接使用系统麦克风和 Windows 中文语音识别引擎（MS-2052-80-DESK）。

**现象 4c：TTS 不朗读**

**根因：** VoiceEngine 默认构造函数传入 `null!` 给 STT 和 TTS，App.xaml.cs 创建的 EdgeTTSEngine 实例从未注入。

**修复：** 
- Edge TTS（zh-CN-XiaoxiaoNeural，pitch +15%，rate -10% 实现萌音）
- Windows TTS（Microsoft Huihui Desktop）作为离线备用

### 问题 5：千千记忆混乱 / 答非所问 / 自称"旺财"

**这是最严重的问题，经历了多轮排查。**

**现象：**
- 千千自称"旺财"（旧名字）
- 千千说"我背后有个超厉害的 Claude 大脑"
- 问"你是谁"回答"我是 Claude Code 的桌面宠物"
- 问"你的 skills"不列出能力只卖萌

**根因分析（按发现顺序）：**

**5a：`--continue` 累积旧上下文**

ProcessManager 使用 `claude -p --continue`，每次对话都延续上一次会话。旧会话中 Claude 自称"旺财"、"Claude"的上下文被不断累积，新 SOUL.MD 人设被海量旧上下文淹没。

→ **修复：去掉 `--continue`。** 每次发独立 `claude -p`，记忆连续性本应由记忆系统提供（后连同记忆系统一起删除）。

**5b：SOUL.MD 格式错误**

原始 SOUL.MD 用"绝对禁止"写法令：
```
❌ 不要提 Claude、GPT、AI模型
❌ 不要提"桌面宠物应用"、"WPF"、"技术栈"
```

Claude 看到这些禁令后，把注意力放在"规则"上而不是"身份"上。回复变成"我收到了你的指令——我会严格遵守你设定的人设规则"——它在遵守规则，而不是成为千千。

→ **修复：** 改为正面自然描述——"你是千千，一只白色像素比熊小狗..."不写任何禁止词。

**5c：knowledge.md 技术细节泄漏**

`memory/knowledge.md` 包含"技术栈: WPF .NET 8, C# 12"、"项目路径: D:\dev\claude-pet"。ContextBuilder 把这些注入 prompt。SOUL.MD 说"不要提技术栈"，但 prompt 自己就在提——自相矛盾。

→ **修复：** knowledge.md 只保留无害内容（"千千是用户的桌面小狗伙伴"）。

**5d：对话历史文件污染**

`memory/conversation/2026-05-29.md` 存了 200+ 行旧对话，Claude 在里面自称"旺财"、"我是桌面宠物"、"帮你和 Claude Code 聊天"。MemoryLoader 读 200 行，ContextBuilder 把整行对话（包括千千的啰嗦回复）塞进 prompt 的"最近对话"。

→ **修复（含 4 项）：**
1. BannedWords 过滤器：跳过含"旺财"、"Claude Code"的条目
2. ReadLastLines(15)：只加载最新 15 行（不是 200 行）
3. ExtractSummary 只提取用户问题，不取千千回复
4. 清空被污染的 conversation 文件

**5e：SOUL.MD 太强调"卖萌"忽略"能力"**

SOUL.MD 写"你不是冷冰冰的工具"导致千千过度卖萌。问"你的 skills"只回"千千在这里呢🐾"不列能力。

→ **修复：** 加"当用户问你能力时，大方展示，别只卖萌"。

**最终决策：删除全部记忆系统和 SOUL.MD。**

经过多轮调试后认识到：记忆系统引入的复杂度（SOUL 人设、上下文拼接、对话保存、压缩整理）导致了严重的身份混乱，收益远小于成本。千千回归本质——Claude Code 的聊天窗口。用户消息直达 `claude -p`，Claude 的完整回复（含思考过程）直接显示。

---

## 四、当前架构

```
用户点击小狗 → 聊天窗弹出 → 输入消息 → PetWindow
  → ClaudeBridge.SendPrompt(msg)
    → ProcessManager: claude -p "msg"（无 --continue，无前缀）
    → Claude Code 处理（含工具调用、思考）
    → 返回完整 stdout
  → BubbleOverlay 显示回复

Claude 状态监控：
  TranscriptWatcher → .jsonl 增量解析 → StateSnapshot
    → StatusBubble（头上气泡：工具名、context%）
    → SpriteSheetPlayer 动画（Think/Talk/Idle）
```

**删除的组件：**
- `src/ClaudePet/Memory/` — MemoryEngine、ContextBuilder、Loader、Writer、Compressor、IdleOrganizer、Models
- `memory/` — soul.md、knowledge.md、preferences.md、tasks.md、conversation/、compressed/

**保留的组件：**
- ClaudeBridge + ProcessManager + TranscriptWatcher
- VoiceEngine（KeyboardWakeDetector + SpeechRecognizer + Edge TTS）
- SpriteSheetPlayer（用户图片 + 程序化动效）
- PetWindow + BubbleOverlay + StatusBubble + TrayManager + SettingsWindow

---

## 五、关键经验教训

1. **`--continue` 是双刃剑。** 它提供连续性但也累积所有历史污染。对于桌面宠物这种需要身份一致性的场景，不应该使用。

2. **SOUL/人设系统不要用"禁止"。** 禁令让 Claude 关注规则本身而非身份。正面自然描述效果远好于否定式约束。

3. **记忆中的技术细节会泄漏到 prompt。** knowledge.md 等技术文件必须精心控制注入内容，否则会和人设自相矛盾。

4. **WinForms 和 WPF 的坐标系统不一致。** 高 DPI 下 `Screen.PrimaryScreen`（物理像素）和 `Window.Left`（设备无关像素）相差缩放倍数。

5. **WPF Keyboard API 需要窗口焦点。** 全局热键必须用 Win32 `GetAsyncKeyState`。

6. **记忆系统看似美好，实际引入大量复杂度。** 文件读写、格式解析、去重、压缩、整理——每层都可能出 bug，且 bug 会直接污染 Claude 的回复质量。简单直接 > 过度设计。

---

## 六、文件清单

| 文件 | 状态 | 说明 |
|------|------|------|
| `src/ClaudePet/App.xaml.cs` | 已修改 | 去掉 MemoryEngine，精简启动流程 |
| `src/ClaudePet/UI/PetWindow.xaml.cs` | 已修改 | 直接调用 ClaudeBridge，去掉记忆上下文 |
| `src/ClaudePet/UI/PetWindow.xaml` | 已修改 | 透明窗口 + StatusBubble + BubbleOverlay |
| `src/ClaudePet/UI/BubbleOverlay.xaml.cs` | 已修改 | 等宽字体显示 Claude 完整回复 |
| `src/ClaudePet/UI/StatusBubble.xaml.cs` | 已修改 | 简化为纯状态指示器 |
| `src/ClaudePet/UI/SettingsWindow.xaml/.cs` | 新增 | 设置面板 |
| `src/ClaudePet/Animation/SpriteSheetPlayer.cs` | 已重写 | 单图 + 程序化动效 |
| `src/ClaudePet/Animation/SpriteGenerator.cs` | 已重写 | 像素风 PNG 生成（后废弃，使用用户图片） |
| `src/ClaudePet/Bridge/ProcessManager.cs` | 已修改 | 去掉 --continue，自动搜索 claude 路径 |
| `src/ClaudePet/Bridge/ClaudeBridge.cs` | 已修改 | 接受 claudePath 参数 |
| `src/ClaudePet/Voice/VoiceEngine.cs` | 已重写 | 系统 SpeechRecognizer + Edge TTS |
| `src/ClaudePet/Voice/KeyboardWakeDetector.cs` | 已重写 | Win32 GetAsyncKeyState |
| `src/ClaudePet/Voice/EdgeTTSEngine.cs` | 已修改 | 萌音参数（pitch+15%, rate-10%） |
| `src/ClaudePet/Voice/WindowsSTTEngine.cs` | 新增 | Windows 系统语音识别 |
| `src/ClaudePet/Voice/WindowsTTSEngine.cs` | 新增 | Windows 系统 TTS（备用） |
| `src/ClaudePet/Memory/` | 已删除 | 全部记忆系统 |
| `memory/` | 已删除 | 全部记忆文件 |
| `docs/superpowers/specs/...memory-system-design.md` | 保留 | 历史设计文档 |
| `docs/superpowers/plans/...memory-system.md` | 保留 | 历史实现计划 |
