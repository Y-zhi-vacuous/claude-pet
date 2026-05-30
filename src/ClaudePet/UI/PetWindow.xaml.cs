using System.Windows;
using System.Windows.Input;
using ClaudePet.Animation;
using ClaudePet.Bridge;
using ClaudePet.Models;
using ClaudePet.Utils;
using ClaudePet.Voice;

namespace ClaudePet.UI;

public partial class PetWindow : Window
{
    private readonly ConfigStore _config;
    private readonly MouseDragHelper _dragHelper;
    private readonly ClaudeBridge _bridge;
    private readonly VoiceEngine _voiceEngine;
    private readonly Window _chatWindow;
    private readonly BubbleOverlay _chatBubble;
    private IAnimationPlayer? _animationPlayer;
    private bool _animationPaused;

    private readonly System.Windows.Threading.DispatcherTimer _idleTimer;
    private DateTime _lastInteraction;
    private bool _isSleeping;

    private double _lastDragX, _lastDragY;
    private bool _wasDragging;

    public bool AnimationPaused
    {
        get => _animationPaused;
        set { _animationPaused = value; if (value) _animationPlayer?.Stop(); else _animationPlayer?.Play(AnimationState.Idle); }
    }

    public bool VoiceEnabled => _voiceEngine.Enabled;

    public PetWindow(ConfigStore config, ClaudeBridge bridge, VoiceEngine voiceEngine)
    {
        _config = config;
        _bridge = bridge;
        _voiceEngine = voiceEngine;
        _dragHelper = new MouseDragHelper(this);
        _lastInteraction = DateTime.Now;

        _idleTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _idleTimer.Tick += OnIdleCheck;

        // 聊天窗口——独立透明 Window，解决 IME 漂移和焦点问题
        _chatBubble = new BubbleOverlay();
        _chatWindow = new Window
        {
            Content = _chatBubble,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            Width = 300,
            Height = 280,
            ShowActivated = true
        };

        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try { InitWindow(); }
        catch (Exception ex) { MessageBox.Show("启动失败:\n" + ex.ToString()); }
    }

    private void InitWindow()
    {
        var petConfig = _config.Load();
        AnimationPaused = petConfig.AnimationPaused;

        _animationPlayer = new SpriteSheetPlayer(PetCanvas);
        _idleTimer.Start();

        // 窗口定位
        if (petConfig.WindowLeft < 0 || petConfig.WindowTop < 0)
        {
            var workArea = System.Windows.SystemParameters.WorkArea;
            double w = ActualWidth > 0 ? ActualWidth : 200;
            double h = ActualHeight > 0 ? ActualHeight : 240;
            Left = workArea.Right - w - 20;
            Top = workArea.Bottom - h - 60;
        }
        else { Left = petConfig.WindowLeft; Top = petConfig.WindowTop; }

        // ─── Claude 状态 → 地板颜色 ───
        _bridge.StateUpdated += (state) =>
        {
            Dispatcher.Invoke(() => UpdateStatusFloor(state));
        };
        UpdateStatusFloor(_bridge.GetCurrentState());

        // ─── 位置变化时同步聊天窗 ───
        LocationChanged += (_, _) => PositionChatWindow();

        // ─── 聊天：直接发给 Claude ───
        _chatBubble.MessageSent += async (msg) =>
        {
            ResetIdle();
            _animationPlayer?.Play(AnimationState.Think);
            _chatBubble.AddSystemMessage("思考中...");

            _chatBubble.AddUserMessage(msg);
            ChatLogger.LogUser(msg);
            try
            {
                var reply = await _bridge.SendPrompt(msg);
                _animationPlayer?.Play(AnimationState.Talk);
                _chatBubble.AddMessage(reply.Trim());
                ChatLogger.LogReply(reply.Trim());
                // TTS 朗读
                Dispatcher.Invoke(() => { StatusFloor.Fill = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x21, 0x96, 0xF3)); StatusFloor.ToolTip = "说话中"; });
                await _voiceEngine.SpeakAsync(reply.Trim());
                Dispatcher.Invoke(() => UpdateStatusFloor(_bridge.GetCurrentState()));
            }
            catch (Exception ex)
            {
                _chatBubble.AddSystemMessage($"错误: {ex.Message}");
            }
            _animationPlayer?.Play(AnimationState.Idle);
        };

        // ─── 关闭按钮 → 隐藏聊天窗 ───
        _chatBubble.CloseRequested += () => _chatWindow.Hide();

        // ─── 麦克风按钮 → 显示聊天窗 ───
        _chatBubble.VoiceRequested += () =>
        {
            ResetIdle();
            ShowChatWindow();
        };

        // ─── Ctrl+Shift+W → 显示聊天窗 ───
        _voiceEngine.ChatRequested += () =>
        {
            Dispatcher.Invoke(() =>
            {
                ResetIdle();
                ShowChatWindow();
                _animationPlayer?.Play(AnimationState.Happy);
                Task.Delay(2000).ContinueWith(_ =>
                    Dispatcher.Invoke(() => _animationPlayer?.Play(AnimationState.Idle)));
            });
        };

        // ─── TTS 朗读 → 说话动画 ───
        _voiceEngine.StateChanged += (state) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (state == VoiceState.Speaking) _animationPlayer?.Play(AnimationState.Talk);
                else if (state == VoiceState.Listening) _animationPlayer?.Play(AnimationState.Idle);
            });
        };

        if (petConfig.VoiceEnabled) _voiceEngine.Enabled = true;
    }

    private void ShowChatWindow()
    {
        PositionChatWindow();
        _chatWindow.Show();
        _chatWindow.Activate();
    }

    private void PositionChatWindow()
    {
        _chatWindow.Left = Left + Width + 10;
        _chatWindow.Top = Top - 30;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        var cfg = _config.Load();
        cfg.WindowLeft = Left; cfg.WindowTop = Top;
        _config.Save(cfg);
        _idleTimer.Stop();
        _chatWindow.Close();
    }

    private void OnIdleCheck(object? sender, EventArgs e)
    {
        var cfg = _config.Load();
        var idleMinutes = cfg.IdleSleepMinutes > 0 ? cfg.IdleSleepMinutes : 5;
        if (!_isSleeping && (DateTime.Now - _lastInteraction).TotalMinutes >= idleMinutes && !_animationPaused)
        {
            _isSleeping = true;
            Dispatcher.Invoke(() => _animationPlayer?.Play(AnimationState.SitSleep));
        }
    }

    private void ResetIdle()
    {
        _lastInteraction = DateTime.Now;
        if (_isSleeping) { _isSleeping = false; if (!_animationPaused) _animationPlayer?.Play(AnimationState.Idle); }
    }

    private void UpdateStatusFloor(StateSnapshot state)
    {
        var color = state.Status switch
        {
            "working" => System.Windows.Media.Color.FromRgb(0xFF, 0xC1, 0x07),
            "error"   => System.Windows.Media.Color.FromRgb(0xF4, 0x43, 0x36),
            _         => System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50),
        };
        StatusFloor.Fill = new System.Windows.Media.SolidColorBrush(color);
        StatusFloor.ToolTip = state.Status switch { "working" => "工作中...", "error" => "出错", _ => "就绪" };
    }

    public void ToggleVoice()
    {
        _voiceEngine.Enabled = !_voiceEngine.Enabled;
        var cfg = _config.Load(); cfg.VoiceEnabled = _voiceEngine.Enabled; _config.Save(cfg);
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ResetIdle();
        if (e.ClickCount == 2)
        {
            if (!_animationPaused)
            {
                _animationPlayer?.Play(AnimationState.Play);
                Task.Delay(3000).ContinueWith(_ => Dispatcher.Invoke(() =>
                { if (!_animationPaused && !_isSleeping) _animationPlayer?.Play(AnimationState.Idle); }));
            }
            return;
        }
        // 单击 → 切换聊天窗
        if (_chatWindow.IsVisible) _chatWindow.Hide(); else ShowChatWindow();
        _dragHelper.OnMouseDown(e.GetPosition(this));
        _lastDragX = Left; _lastDragY = Top; _wasDragging = false;
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        _dragHelper.OnMouseMove(e.GetPosition(this));
        if (!_animationPaused && _animationPlayer != null)
        {
            double dx = Left - _lastDragX, dy = Top - _lastDragY;
            _lastDragX = Left; _lastDragY = Top;
            if (Math.Abs(dx) > 1 || Math.Abs(dy) > 1)
            {
                _wasDragging = true;
                var ws = Math.Abs(dx) >= Math.Abs(dy)
                    ? (dx > 0 ? AnimationState.WalkRight : AnimationState.WalkLeft)
                    : (dy > 0 ? AnimationState.WalkDown : AnimationState.WalkUp);
                _animationPlayer.Play(ws);
            }
        }
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragHelper.OnMouseUp();
        if (_wasDragging && !_animationPaused) _animationPlayer?.Play(AnimationState.Idle);
        _wasDragging = false;
    }

    private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e) { }

    private void Canvas_DragEnter(object sender, DragEventArgs e) =>
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    private void Canvas_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Canvas_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (files == null || files.Length == 0) return;
        ResetIdle();
        _animationPlayer?.Play(AnimationState.Eat);
        ShowChatWindow();
        var prompt = $"帮我分析以下文件:\n{string.Join("\n", files.Select(f => $"- {f}"))}";
        _chatBubble.AddSystemMessage($"[拖入了 {files.Length} 个文件]");
        await Task.Delay(1500);
        _animationPlayer?.Play(AnimationState.Think);
        try
        {
            var reply = await _bridge.SendPrompt(prompt);
            _animationPlayer?.Play(AnimationState.Talk);
            _chatBubble.AddMessage(reply.Trim());
            _animationPlayer?.Play(AnimationState.Happy);
        }
        catch (Exception ex) { _chatBubble.AddSystemMessage($"错误: {ex.Message}"); _animationPlayer?.Play(AnimationState.Idle); }
    }

    public void OpenSettings()
    {
        var cfg = _config.Load();
        var sw = new SettingsWindow(cfg, (newCfg) =>
        {
            _config.Save(newCfg);
            if (newCfg.VoiceEnabled != _voiceEngine.Enabled) _voiceEngine.Enabled = newCfg.VoiceEnabled;
        });
        sw.Owner = this;
        sw.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        sw.ShowDialog();
    }
}
