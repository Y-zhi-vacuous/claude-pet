# 千千桌宠

一只漂浮在 Windows 桌面上的像素小狗，作为 Claude Code 的桌面交互界面。

<img src="screenshot.png" width="200" />

## 功能

- 🐶 像素帧动画（11 个状态：待机/行走/睡觉/玩耍/思考/说话/开心/吃东西）
- 💬 聊天窗口直接与 Claude Code 对话，支持 Markdown 渲染
- 🔊 TTS 语音朗读回复（Edge TTS 萌音）
- ⌨️ `Ctrl+Shift+W` 快捷唤醒
- 📁 拖拽文件自动分析
- ⚙️ 系统托盘 + 设置面板
- 📝 聊天记录自动保存

## 快速开始

### 方式一：安装包（推荐）

下载最新 Release 中的 `千千安装包.zip`，解压后双击 `千千安装向导.exe`，按 7 步向导完成安装。

### 方式二：源码运行

```bash
# 前提
# - .NET 8 SDK
# - Claude Code CLI (npm install -g @anthropic-ai/claude-code)

git clone git@github.com:Y-zhi-vacuous/claude-pet.git
cd claude-pet
dotnet run --project src/ClaudePet
```

## 构建安装包

```bash
powershell -File build-installer.ps1
# 输出: publish/Setup/千千安装向导.exe
```

## 技术栈

- .NET 8 + WPF + C# 12
- Claude Code CLI (`claude -p`)
- Edge TTS（语音朗读）
- Newtonsoft.Json / NAudio

## 项目结构

```
src/
├── ClaudePet/          # 千千主程序
│   ├── Animation/      # 精灵帧动画
│   ├── Bridge/         # Claude Code 通信
│   ├── Models/         # 数据模型
│   ├── UI/             # 界面（PetWindow/BubbleOverlay/Settings）
│   ├── Utils/          # 工具（Config/ChatLog/Drag）
│   └── Voice/          # 语音引擎（TTS+唤醒）
├── Setup/              # 安装向导
assets/sprites/         # 11 状态精灵帧 PNG
docs/                   # 设计文档
```

## License

MIT
