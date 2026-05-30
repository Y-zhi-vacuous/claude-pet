using System.Drawing;
using System.Windows.Forms;
using ClaudePet.Utils;

namespace ClaudePet.UI;

public class TrayManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;

    public event Action? SettingsRequested;
    public event Action? ToggleVoiceRequested;
    public event Action? SwitchSessionRequested;

    public TrayManager()
    {
        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.Add("切换会话", null, (_, _) => SwitchSessionRequested?.Invoke());
        _contextMenu.Items.Add("暂停动画", null, OnPauseAnimation);
        _contextMenu.Items.Add("开启语音", null, OnToggleVoice);
        _contextMenu.Items.Add("-");
        _contextMenu.Items.Add("设置", null, (_, _) => SettingsRequested?.Invoke());
        _contextMenu.Items.Add("退出", null, OnExit);

        _notifyIcon = new NotifyIcon
        {
            Text = "千千 - Claude 小狗桌宠",
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        // 用橙色圆形占位图标
        using var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(System.Drawing.Color.Transparent);
        using var brush = new SolidBrush(System.Drawing.Color.DarkOrange);
        g.FillEllipse(brush, 2, 2, 28, 28);
        var iconHandle = bitmap.GetHicon();
        _notifyIcon.Icon = Icon.FromHandle(iconHandle);
    }

    // ─── 托盘菜单状态更新 ───
    public void UpdatePauseText(bool isPaused)
    {
        var item = (ToolStripMenuItem?)_contextMenu.Items[1];
        if (item != null)
            item.Text = isPaused ? "恢复动画" : "暂停动画";
    }

    public void UpdateVoiceText(bool voiceEnabled)
    {
        var item = (ToolStripMenuItem?)_contextMenu.Items[2];
        if (item != null)
            item.Text = voiceEnabled ? "关闭语音" : "开启语音";
    }

    public void SetTooltip(string text)
    {
        _notifyIcon.Text = text;
    }

    // ─── 菜单事件处理 ───
    private void OnPauseAnimation(object? sender, EventArgs e)
    {
        var window = Application.Current.Windows.OfType<PetWindow>().FirstOrDefault();
        if (window != null)
        {
            window.AnimationPaused = !window.AnimationPaused;
            UpdatePauseText(window.AnimationPaused);
        }
    }

    private void OnToggleVoice(object? sender, EventArgs e)
    {
        var window = Application.Current.Windows.OfType<PetWindow>().FirstOrDefault();
        if (window != null)
        {
            window.ToggleVoice();
            UpdateVoiceText(window.VoiceEnabled);
        }
    }

    private void OnExit(object? sender, EventArgs e)
    {
        _notifyIcon.Visible = false;
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
    }
}
