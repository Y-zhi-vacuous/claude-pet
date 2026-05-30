using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Setup;

public partial class MainWindow : Window
{
    private int _step = 1;
    private string _petName = "千千";
    private string _apiKey = "";
    private string _modelName = "claude-sonnet-4-6";
    private string _provider = "Anthropic";
    private string _baseUrl = "";
    private string _installDir = "";
    private readonly Dictionary<int, string> _stepTitles = new()
    {
        [1] = "欢迎", [2] = "环境检测", [3] = "桌宠取名",
        [4] = "安装 Claude", [5] = "模型配置", [6] = "安装中", [7] = "完成"
    };

    public MainWindow()
    {
        InitializeComponent();
        _installDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "千千桌宠");
        ShowPage(1);
    }

    private void ShowPage(int step)
    {
        _step = step;
        StepLabel.Text = $"步骤 {step}/7";
        StepTitle.Text = _stepTitles[step];
        PageContent.Children.Clear();

        BtnBack.Visibility = step > 1 ? Visibility.Visible : Visibility.Collapsed;
        BtnNext.Visibility = step < 7 ? Visibility.Visible : Visibility.Collapsed;
        BtnNext.Content = step == 6 ? "安装" : "下一步";
        BtnFinish.Visibility = step == 7 ? Visibility.Visible : Visibility.Collapsed;

        switch (step)
        {
            case 1: ShowWelcome(); break;
            case 2: ShowEnvCheck(); break;
            case 3: ShowPetName(); break;
            case 4: ShowClaudeInstall(); break;
            case 5: ShowModelConfig(); break;
            case 6: ShowInstall(); break;
            case 7: ShowDone(); break;
        }
    }

    // ─── Page 1: 欢迎 ───
    private void ShowWelcome()
    {
        AddText("欢迎安装千千桌宠", 18, Colors.White, true);
        AddSpace();
        AddText("千千是一只桌面像素比熊小狗，作为 Claude Code 的桌面伴侣。", 13, Color.FromRgb(0xAA, 0xAA, 0xAA));
        AddText("安装向导将引导你完成设置，整个过程大约需要 3 分钟。", 13, Color.FromRgb(0xAA, 0xAA, 0xAA));
        AddSpace();
        AddText("需要联网以下载必要的运行时组件。", 11, Color.FromRgb(0x88, 0x88, 0x88));
    }

    // ─── Page 2: 环境检测 ───
    private async void ShowEnvCheck()
    {
        var status = AddText("正在检测环境...", 14, Colors.White);
        BtnNext.IsEnabled = false;

        // .NET Runtime
        var dotnetOk = await Task.Run(() => CheckDotNet());
        AddText(dotnetOk ? "✓ .NET 8 Desktop Runtime — 已安装" : "✗ .NET 8 Desktop Runtime — 未安装",
            12, dotnetOk ? Colors.LimeGreen : Colors.Orange);
        if (!dotnetOk)
        {
            AddText("  正在下载 .NET Runtime...", 11, Color.FromRgb(0x88, 0x88, 0x88));
            var ok = await Task.Run(() => InstallDotNet());
            AddText(ok ? "  ✓ 安装完成" : "  ✗ 安装失败，请手动下载: https://dotnet.microsoft.com/download",
                11, ok ? Colors.LimeGreen : Colors.Red);
        }

        // Node.js
        var nodeOk = await Task.Run(() => CheckNode());
        AddText(nodeOk ? "✓ Node.js — 已安装" : "✗ Node.js — 未安装",
            12, nodeOk ? Colors.LimeGreen : Colors.Orange);
        if (!nodeOk)
        {
            AddText("  正在下载 Node.js...", 11, Color.FromRgb(0x88, 0x88, 0x88));
            var ok = await Task.Run(() => InstallNode());
            AddText(ok ? "  ✓ 安装完成" : "  ✗ 安装失败，请手动下载: https://nodejs.org",
                11, ok ? Colors.LimeGreen : Colors.Red);
        }

        status.Text = "环境检测完成";
        BtnNext.IsEnabled = true;
    }

    // ─── Page 3: 取名 ───
    private void ShowPetName()
    {
        AddText("给你的桌宠取个名字吧", 14, Colors.White);
        var tb = new TextBox
        {
            Text = _petName,
            FontSize = 16,
            Foreground = new SolidColorBrush(Colors.White),
            Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 12, 0, 0),
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        tb.TextChanged += (_, _) => _petName = tb.Text;
        PageContent.Children.Add(tb);
    }

    // ─── Page 4: Claude 安装 ───
    private async void ShowClaudeInstall()
    {
        var status = AddText("检测 Claude Code CLI...", 14, Colors.White);
        BtnNext.IsEnabled = false;

        var ok = await Task.Run(() => CheckClaude());
        if (ok)
        {
            status.Text = "✓ Claude Code CLI — 已安装";
            status.Foreground = new SolidColorBrush(Colors.LimeGreen);
            BtnNext.IsEnabled = true;
        }
        else
        {
            status.Text = "正在安装 Claude Code CLI...";
            AddText("npm install -g @anthropic-ai/claude-code", 11, Color.FromRgb(0x88, 0x88, 0x88));
            var installed = await Task.Run(() => InstallClaude());
            status.Text = installed ? "✓ Claude Code 安装完成" : "✗ 安装失败，请打开命令行执行: npm install -g @anthropic-ai/claude-code";
            status.Foreground = new SolidColorBrush(installed ? Colors.LimeGreen : Colors.Red);
            BtnNext.IsEnabled = true;
        }
    }

    // ─── Page 5: 模型配置 ───
    private void ShowModelConfig()
    {
        AddText("模型配置", 14, Colors.White);
        AddSpace(6);

        // 供应商
        AddText("模型供应商", 12, Color.FromRgb(0xAA, 0xAA, 0xAA));
        var cb = new ComboBox
        {
            ItemsSource = new[] { "Anthropic", "OpenAI", "智谱 (GLM)", "自定义" },
            SelectedItem = _provider,
            FontSize = 13,
            Foreground = new SolidColorBrush(Colors.White),
            Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 4, 0, 8),
            Width = 250,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        cb.SelectionChanged += (_, _) =>
        {
            _provider = cb.SelectedItem?.ToString() ?? "Anthropic";
            _modelName = _provider switch
            {
                "Anthropic" => "claude-sonnet-4-6",
                "OpenAI" => "gpt-4o",
                "智谱 (GLM)" => "glm-4.1v-flash",
                _ => ""
            };
            _baseUrl = _provider switch
            {
                "智谱 (GLM)" => "https://open.bigmodel.cn/api/paas/v4",
                _ => ""
            };
            // 重新渲染模型名
            ShowModelConfig();
        };
        PageContent.Children.Add(cb);

        // API Key
        AddText("API Key", 12, Color.FromRgb(0xAA, 0xAA, 0xAA));
        var keyBox = new PasswordBox
        {
            Password = _apiKey,
            FontSize = 13,
            Foreground = new SolidColorBrush(Colors.White),
            Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 4, 0, 8),
            Width = 300,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        keyBox.PasswordChanged += (_, _) => _apiKey = keyBox.Password;
        PageContent.Children.Add(keyBox);

        // 模型名
        AddText("模型名称", 12, Color.FromRgb(0xAA, 0xAA, 0xAA));
        var modelBox = new TextBox
        {
            Text = _modelName,
            FontSize = 13,
            Foreground = new SolidColorBrush(Colors.White),
            Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 4, 0, 0),
            Width = 300,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        modelBox.TextChanged += (_, _) => _modelName = modelBox.Text;
        PageContent.Children.Add(modelBox);

        // Base URL (custom only)
        if (_provider == "自定义" || _provider == "智谱 (GLM)")
        {
            AddText("Base URL", 12, Color.FromRgb(0xAA, 0xAA, 0xAA));
            var urlBox = new TextBox
            {
                Text = _baseUrl,
                FontSize = 13,
                Foreground = new SolidColorBrush(Colors.White),
                Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 4, 0, 0),
                Width = 300,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            urlBox.TextChanged += (_, _) => _baseUrl = urlBox.Text;
            PageContent.Children.Add(urlBox);
        }
    }

    // ─── Page 6: 安装 ───
    private async void ShowInstall()
    {
        var pb = new ProgressBar
        {
            IsIndeterminate = true,
            Height = 8,
            Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
            Margin = new Thickness(0, 0, 0, 12)
        };
        PageContent.Children.Add(pb);
        var status = AddText("正在安装千千...", 14, Colors.White);
        BtnNext.IsEnabled = false;

        var result = await Task.Run(() => DoInstall());
        pb.IsIndeterminate = false;
        status.Text = result ? "✓ 安装完成！" : "✗ 安装失败";
        status.Foreground = new SolidColorBrush(result ? Colors.LimeGreen : Colors.Red);
        BtnNext.IsEnabled = true;
    }

    // ─── Page 7: 完成 ───
    private void ShowDone()
    {
        AddText("🎉 千千已就绪！", 18, Colors.White, true);
        AddSpace();
        AddText($"安装位置: {_installDir}", 12, Color.FromRgb(0xAA, 0xAA, 0xAA));
        AddText($"桌宠名字: {_petName}", 12, Color.FromRgb(0xAA, 0xAA, 0xAA));
        AddSpace();
        AddText("桌面快捷方式已创建，双击即可启动千千。", 12, Color.FromRgb(0x88, 0x88, 0x88));
    }

    // ─── 按钮事件 ───
    private void BtnBack_Click(object sender, RoutedEventArgs e) => ShowPage(_step - 1);

    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        ShowPage(_step + 1);
    }

    private void BtnFinish_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var exe = Path.Combine(_installDir, "ClaudePet.exe");
            if (File.Exists(exe))
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        }
        catch { }
        Application.Current.Shutdown();
    }

    // ─── 环境检测方法 ───
    private static bool CheckDotNet()
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", "--list-runtimes")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            var p = Process.Start(psi);
            p!.WaitForExit(5000);
            var output = p.StandardOutput.ReadToEnd();
            return output.Contains("Microsoft.WindowsDesktop.App 8.");
        }
        catch { return false; }
    }

    private static bool InstallDotNet()
    {
        try
        {
            var url = "https://download.visualstudio.microsoft.com/download/pr/2d0a4b5c-3d3d-4d3d-8d3d-3d3d3d3d3d3d/5d5d5d5d5d5d5d5d5d5d5d5d5d5d5d5d/windowsdesktop-runtime-8.0.11-win-x64.exe";
            var tmp = Path.GetTempFileName() + ".exe";
            using var client = new HttpClient();
            var data = client.GetByteArrayAsync(url).Result;
            File.WriteAllBytes(tmp, data);
            var psi = new ProcessStartInfo(tmp, "/install /quiet /norestart")
            { UseShellExecute = true, CreateNoWindow = true };
            var p = Process.Start(psi);
            p!.WaitForExit(120000);
            File.Delete(tmp);
            return CheckDotNet();
        }
        catch { return false; }
    }

    private static bool CheckNode()
    {
        try
        {
            var psi = new ProcessStartInfo("node", "--version")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            var p = Process.Start(psi);
            p!.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    private static bool InstallNode()
    {
        try
        {
            var url = "https://nodejs.org/dist/v20.18.0/node-v20.18.0-x64.msi";
            var tmp = Path.Combine(Path.GetTempPath(), "node-install.msi");
            using var client = new HttpClient();
            var data = client.GetByteArrayAsync(url).Result;
            File.WriteAllBytes(tmp, data);
            var psi = new ProcessStartInfo("msiexec", $"/i \"{tmp}\" /quiet /norestart")
            { UseShellExecute = true, CreateNoWindow = true };
            var p = Process.Start(psi);
            p!.WaitForExit(120000);
            File.Delete(tmp);
            return CheckNode();
        }
        catch { return false; }
    }

    private static bool CheckClaude()
    {
        try
        {
            var psi = new ProcessStartInfo("claude", "--version")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            var p = Process.Start(psi);
            p!.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    private static bool InstallClaude()
    {
        try
        {
            var psi = new ProcessStartInfo("npm", "install -g @anthropic-ai/claude-code")
            { UseShellExecute = true, CreateNoWindow = true };
            var p = Process.Start(psi);
            p!.WaitForExit(120000);
            return CheckClaude();
        }
        catch { return false; }
    }

    // ─── 文件安装 ───
    private bool DoInstall()
    {
        try
        {
            Directory.CreateDirectory(_installDir);

            // 从嵌入资源释放千千文件
            var asm = Assembly.GetExecutingAssembly();
            foreach (var res in asm.GetManifestResourceNames())
            {
                if (!res.StartsWith("Setup.")) continue; // 跳过非嵌入资源
                // 提取真实文件路径
                var fileName = res.Replace("Setup.", "").Replace(".resources", "");
                if (string.IsNullOrEmpty(fileName)) continue;

                var targetPath = Path.Combine(_installDir, fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                using var stream = asm.GetManifestResourceStream(res);
                if (stream == null) continue;
                using var fs = File.Create(targetPath);
                stream.CopyTo(fs);
            }

            // 如果嵌入资源为空，复制 publish 目录
            var publishDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "publish", "ClaudePet");
            if (Directory.Exists(publishDir))
            {
                CopyDirectory(publishDir, _installDir);
            }

            // 写 config.json
            var config = new { PetName = _petName, WindowLeft = -1.0, WindowTop = -1.0,
                AnimationPaused = false, VoiceEnabled = true, WakeWord = "嘿小狗",
                IdleSleepMinutes = 5, WorkingDirectory = ".", TranscriptsDirectory = "" };
            File.WriteAllText(Path.Combine(_installDir, "config.json"),
                JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }),
                Encoding.UTF8);

            // 写 Claude 配置
            WriteClaudeConfig();

            // 桌面快捷方式
            CreateShortcut();

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Install error: {ex}");
            return false;
        }
    }

    private void WriteClaudeConfig()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var claudeDir = Path.Combine(home, ".claude");
        Directory.CreateDirectory(claudeDir);

        // settings.json
        var settingsPath = Path.Combine(claudeDir, "settings.json");
        var settings = new Dictionary<string, object>();
        if (File.Exists(settingsPath))
        {
            try { settings = JsonSerializer.Deserialize<Dictionary<string, object>>(
                File.ReadAllText(settingsPath)) ?? new(); } catch { }
        }

        var profile = new Dictionary<string, object> { ["model"] = _modelName };
        if (_provider != "Anthropic")
        {
            profile["apiKeyHelper"] = $"echo {_apiKey}";
            if (!string.IsNullOrEmpty(_baseUrl))
                profile["baseUrl"] = _baseUrl;
        }
        settings["profiles"] = new Dictionary<string, object> { ["default"] = profile };

        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings,
            new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);

        // credentials（仅 Anthropic）
        if (_provider == "Anthropic" && !string.IsNullOrEmpty(_apiKey))
        {
            File.WriteAllText(Path.Combine(claudeDir, "credentials"), _apiKey, Encoding.UTF8);
        }
    }

    private void CreateShortcut()
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var shortcutPath = Path.Combine(desktop, "千千.lnk");
            var targetPath = Path.Combine(_installDir, "ClaudePet.exe");

            // 用 PowerShell 创建快捷方式
            var psi = new ProcessStartInfo("powershell", $"-Command \""
                + $"$ws = New-Object -ComObject WScript.Shell; "
                + $"$s = $ws.CreateShortcut('{shortcutPath.Replace("'", "''")}'); "
                + $"$s.TargetPath = '{targetPath.Replace("'", "''")}'; "
                + $"$s.WorkingDirectory = '{_installDir.Replace("'", "''")}'; "
                + $"$s.Description = '千千桌宠'; $s.Save()\"")
            { UseShellExecute = false, CreateNoWindow = true };
            var p = Process.Start(psi);
            p!.WaitForExit(5000);
        }
        catch { }
    }

    private static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(src))
            CopyDirectory(dir, Path.Combine(dst, Path.GetFileName(dir)));
    }

    // ─── UI 辅助 ───
    private void AddSpace(int height = 8) => AddText("", height, Colors.Transparent);
    private TextBlock AddText(string text, int fontSize, Color color, bool bold = false)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            Foreground = new SolidColorBrush(color),
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 1, 0, 1)
        };
        PageContent.Children.Add(tb);
        return tb;
    }
}
