# 千千安装包 — 实现计划

> **For agentic workers:** Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 产出单个 `千千安装包.exe`，双击后 7 步向导完成安装。

**Architecture:** 新增独立 WPF 项目 `src/Setup/`，千千发布文件作为 EmbeddedResource 嵌入，运行时释放。不修改千千主项目。

**Tech Stack:** WPF .NET 8, C# 12, PowerShell（构建脚本）

---

## 文件结构

```
新增:
  src/Setup/
    ├── Setup.csproj           # WPF 项目
    ├── App.xaml/.cs           # 入口
    ├── MainWindow.xaml/.cs    # 7 步向导 UI + 逻辑
    └── InstallerEngine.cs     # 环境检测 + 文件释放 + 配置写入
  build-installer.ps1          # 一键构建脚本

修改:
  无（千千主项目不动）
```

---

### Task 1: 创建 Setup 项目骨架

**Files:**
- Create: `src/Setup/Setup.csproj`
- Create: `src/Setup/App.xaml`
- Create: `src/Setup/App.xaml.cs`

- [ ] **Step 1: 创建 Setup.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>千千安装向导</AssemblyName>
    <ApplicationIcon>../../assets/icon.ico</ApplicationIcon>
  </PropertyGroup>
  <ItemGroup>
    <EmbeddedResource Include="..\..\publish\**\*" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: 创建 App.xaml + App.xaml.cs**

```xml
<Application x:Class="Setup.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
</Application>
```

```csharp
namespace Setup;
public partial class App : Application { }
```

- [ ] **Step 3: 编译验证**

```bash
dotnet build src/Setup/Setup.csproj
```

---

### Task 2: 创建 MainWindow — 7 步向导

**Files:**
- Create: `src/Setup/MainWindow.xaml`
- Create: `src/Setup/MainWindow.xaml.cs`

完整 WPF 窗口，左侧千千 Logo，右侧 7 页内容切换（欢迎/环境/取名/Claude/模型/安装/完成）。含进度条、文本框、下拉框、按钮。所有逻辑与 UI 绑在一起实现。

---

### Task 3: 创建 InstallerEngine

**Files:**
- Create: `src/Setup/InstallerEngine.cs`

实现：
- `CheckDotNet()` / `InstallDotNet()` — 注册表检测 + 下载静默安装
- `CheckNode()` / `InstallNode()` — cmd 检测 + 下载静默安装
- `CheckClaude()` / `InstallClaude()` — cmd 检测 + npm install
- `ExtractFiles(string targetDir)` — 从 EmbeddedResource 释放千千文件
- `WriteClaudeConfig(supplier, apiKey, model)` — 写入 ~/.claude/settings.json
- `CreateShortcut(targetDir)` — 创建桌面快捷方式

---

### Task 4: 创建构建脚本

**Files:**
- Create: `build-installer.ps1`

```powershell
# 1. 发布千千
dotnet publish src/ClaudePet -c Release -o publish/ClaudePet

# 2. 构建 Setup
dotnet publish src/Setup -c Release -o publish/Setup

# 3. 输出
Write-Host "安装包: publish/Setup/千千安装向导.exe"
```

---

### Task 5: 构建安装包 + 测试

- [ ] 执行构建脚本
- [ ] 验证 Setup.exe 可运行
- [ ] 验证环境检测
- [ ] 验证文件释放
- [ ] 验证配置写入

---

### Task 6: Git 初始化并推送

```bash
cd D:/dev/claude-pet
git init
git add .
git commit -m "千千桌宠 v1.0 — 含安装包系统"
git remote add origin https://github.com/Y-zhi-vacuous/claude-pet.git
git push -u origin main
```
