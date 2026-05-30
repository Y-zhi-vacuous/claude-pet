using System.Windows;
using ClaudePet.Animation;
using ClaudePet.Bridge;
using ClaudePet.UI;
using ClaudePet.Utils;
using ClaudePet.Voice;

namespace ClaudePet;

public partial class App : Application
{
    private TrayManager? _trayManager;
    private PetWindow? _petWindow;
    private VoiceEngine? _voiceEngine;
    private ClaudeBridge? _bridge;
    private string _claudePath = "claude";
    private string _workDir = ".";

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            while (!File.Exists(Path.Combine(baseDir, "config.json")) && baseDir.Length > 3)
                baseDir = Path.GetDirectoryName(baseDir)!;

            var config = new ConfigStore(baseDir);
            var petConfig = config.Load();

            // 精灵图片
            var assetsDir = Path.Combine(baseDir, "assets", "sprites");
            SpriteGenerator.GenerateAllFrames(assetsDir);
            AnimationCatalog.SetAssetsDirectory(assetsDir);

            // Claude 通信
            _claudePath = ProcessManager.FindClaudePath();
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var transcriptsDir = Path.Combine(home, ".claude", "projects");
            _workDir = string.IsNullOrEmpty(petConfig.WorkingDirectory) || petConfig.WorkingDirectory == "."
                ? baseDir : Path.GetFullPath(Path.Combine(baseDir, petConfig.WorkingDirectory));

            _bridge = new ClaudeBridge(_workDir, transcriptsDir, _claudePath);

            // 语音
            _voiceEngine = new VoiceEngine();
            if (petConfig.VoiceEnabled) _voiceEngine.Enabled = true;

            // 托盘
            _trayManager = new TrayManager();
            _trayManager.SettingsRequested += () =>
                Dispatcher.Invoke(() => _petWindow?.OpenSettings());
            _trayManager.SwitchSessionRequested += () =>
            {
                Dispatcher.Invoke(() =>
                    MessageBox.Show("已切换到新会话", "千千",
                        MessageBoxButton.OK, MessageBoxImage.Information));
            };

            // 主窗口
            _petWindow = new PetWindow(config, _bridge, _voiceEngine);
            _petWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show("启动失败:\n" + ex, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        _trayManager?.Dispose();
        _voiceEngine?.Dispose();
        _bridge?.Dispose();
    }
}
