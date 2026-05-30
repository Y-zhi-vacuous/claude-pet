using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClaudePet.Models;
using Color = System.Windows.Media.Color;

namespace ClaudePet.UI;

public partial class HudMiniBar : UserControl
{
    public HudMiniBar()
    {
        InitializeComponent();
    }

    public void UpdateState(StateSnapshot state)
    {
        Dispatcher.Invoke(() =>
        {
            var model = string.IsNullOrEmpty(state.Model) ? "等待连接..." : state.Model;
            ModelLabel.Text = state.Status switch
            {
                "working" => $"[{model}] ● 工作中...",
                "error"   => $"[{model}] ✘ 出错",
                _         => $"[{model}] ✓ 就绪"
            };

            var pct = Math.Clamp(state.ContextPercent, 0, 100);
            ContextBar.Width = pct / 100.0 * 168;

            ContextBar.Fill = pct switch
            {
                >= 85 => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
                >= 70 => new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)),
                _      => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
            };

            ModelLabel.Text += $" | Context {pct:F0}%";
        });
    }
}
