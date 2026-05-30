# 千千安装包 — 设计文档

> 将千千工程封装为单个 Setup.exe 安装包，支持环境检测、依赖安装、模型配置、桌宠取名。

---

## 1. 概述

### 目标
产出单个 `千千安装包.exe`，用户双击后通过 7 步向导完成安装，安装后即可使用千千。

### 打包方式
- WPF 安装向导程序（`Setup.exe`），千千发布文件作为内嵌资源
- 运行时释放资源到安装目录
- 依赖走在线下载（.NET Runtime、Node.js、Claude CLI）

---

## 2. 打包结构

```
千千安装包.exe  (自解压引导程序 ≈30MB)
└── 内嵌资源:
    ├── 千千发布文件/              ← dotnet publish -c Release 输出
    │   ├── ClaudePet.exe
    │   ├── *.dll
    │   └── assets/sprites/       ← 11 状态 × 4-6 帧 PNG
    ├── InstallClaude.ps1         ← npm install -g 脚本
    └── 千千.ico                  ← 桌面快捷方式图标
```

### 构建流程
1. `dotnet publish src/ClaudePet -c Release -o publish/` → 千千发布文件
2. 收集 `publish/` 下全部文件作为 `Setup` 项目的 `EmbeddedResource`
3. `dotnet publish src/Setup -c Release` → 产出 `千千安装包.exe`

### 推荐安装路径
`%LOCALAPPDATA%\千千桌宠\`

---

## 3. 安装向导 UI

7 页，左侧 96×96 千千 Logo，右侧页面内容。

| 页面 | 内容 |
|------|------|
| **1. 欢迎** | Logo + "欢迎安装千千桌宠" + 版本号 + "下一步" |
| **2. 环境检测** | 逐项检测进度条——.NET 8 Desktop Runtime / Node.js / Claude CLI。缺什么自动下载静默安装，失败则显示手动下载链接 |
| **3. 取名** | 文本框，默认"千千"。写入 config.json |
| **4. Claude 安装** | 检测 `claude --version`。不可用则点"安装"执行 `npm install -g @anthropic-ai/claude-code` |
| **5. 模型配置** | 下拉选供应商 → 输入 API Key → 模型名自动推荐可手动改 → 写入 `~/.claude/settings.json` |
| **6. 安装进度** | 进度条 + "正在安装千千..."，释放嵌入资源到安装目录 |
| **7. 完成** | "千千已就绪！" + 创建桌面快捷方式 + "启动千千"按钮 |

### 第 5 步详细字段

| 字段 | 类型 | 默认值 |
|------|------|--------|
| 模型供应商 | 下拉框 (Anthropic/OpenAI/智谱/自定义) | Anthropic |
| API Key | 密码输入框 | 空 |
| 模型名称 | 文本框（含根据供应商自动填充） | claude-sonnet-4-6 |
| Base URL (自定义时显示) | 文本框 | 空 |

---

## 4. 环境检测与安装

| 检测项 | 方法 | 未安装时的操作 |
|--------|------|--------------|
| .NET 8 Desktop Runtime | 查注册表 | 从微软 CDN 下载 `windowsdesktop-runtime-8.x.x-win-x64.exe` 静默安装 |
| Node.js | `node --version` | 从 nodejs.org 下载 LTS `.msi` 静默安装 |
| Claude Code CLI | `claude --version` | `npm install -g @anthropic-ai/claude-code` |

### 下载源
- .NET: `https://dotnet.microsoft.com/download/dotnet/8.0/...`
- Node.js: `https://nodejs.org/dist/v20.x/node-v20.x.x-x64.msi`

### 静默安装
```
.NET:  windowsdesktop-runtime-8.x.x-win-x64.exe /install /quiet /norestart
Node:  msiexec /i node-v20.x.x-x64.msi /quiet /norestart
Claude: npm install -g @anthropic-ai/claude-code
```

---

## 5. Claude 配置写入

安装向导读取/创建 `~/.claude/settings.json`，根据用户选择写入配置：

### Anthropic
```json
{
  "profiles": {
    "default": {
      "model": "claude-sonnet-4-6"
    }
  }
}
```
API Key 写入 `~/.claude/credentials`

### 第三方（OpenAI/智谱/自定义）
```json
{
  "profiles": {
    "default": {
      "model": "glm-4.1v-flash",
      "apiKeyHelper": "echo sk-xxx...",
      "baseUrl": "https://open.bigmodel.cn/api/paas/v4"
    }
  }
}
```

### 写入策略
- 读取已有 settings.json → 合并新参数 → 写回（不覆盖已有配置）
- API Key 写 credentials 文件

---

## 6. 安装目录结构

```
%LOCALAPPDATA%\千千桌宠\
├── ClaudePet.exe
├── ClaudePet.dll
├── ... (依赖 DLL)
├── assets\
│   └── sprites\       ← 11 动画状态 PNG 序列帧
├── config.json         ← 安装时生成（含 PetName 等）
└── chat_history.md     ← 运行时生成
```

---

## 7. 文件变更

```
新增:
  src/Setup/
    ├── Setup.csproj           # WPF 安装向导项目
    ├── App.xaml/.cs           # 入口
    ├── MainWindow.xaml/.cs    # 7 页向导 UI
    ├── InstallerEngine.cs     # 环境检测 + 文件释放 + 配置写入
    └── EmbeddedResources/     # build 时拷贝的千千发布文件

修改:
  (无) — 千千主项目不变

构建脚本:
  build-installer.ps1          # 一键构建安装包
```

---

## 8. 验证检查单

- [ ] `千千安装包.exe` 可双击启动
- [ ] 第 2 步正确检测 .NET Runtime / Node.js / Claude
- [ ] 缺少 .NET Runtime 时自动下载安装
- [ ] 缺少 Node.js 时自动下载安装
- [ ] 缺少 Claude CLI 时 `npm install -g` 成功
- [ ] 第 3 步输入的名字写入 config.json
- [ ] 第 5 步 API Key + 模型正确写入 `~/.claude/settings.json`
- [ ] 第 6 步文件正确释放到安装目录
- [ ] 第 7 步桌面快捷方式创建成功，路径指向安装目录下的 ClaudePet.exe
- [ ] 点击快捷方式可正常启动千千
- [ ] 启动后聊天功能正常（claude -p 可用）
