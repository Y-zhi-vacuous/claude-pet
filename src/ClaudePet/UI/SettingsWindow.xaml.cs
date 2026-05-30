using System.Windows;
using ClaudePet.Models;

namespace ClaudePet.UI;

public partial class SettingsWindow : Window
{
    private readonly PetConfig _config;
    private readonly Action<PetConfig> _onSave;

    public SettingsWindow(PetConfig config, Action<PetConfig> onSave)
    {
        _config = config;
        _onSave = onSave;
        InitializeComponent();

        // 加载当前配置
        PetNameBox.Text = config.PetName;
        WakeWordBox.Text = config.WakeWord;
        SleepSlider.Value = config.IdleSleepMinutes;
        SleepSlider.ValueChanged += (_, _) =>
        {
            var val = (int)SleepSlider.Value;
            SleepLabel.Text = $"{val}分";
        };
        SleepLabel.Text = $"{config.IdleSleepMinutes}分";
        VoiceCheckBox.IsChecked = config.VoiceEnabled;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _config.PetName = PetNameBox.Text.Trim();
        _config.WakeWord = WakeWordBox.Text.Trim();
        _config.IdleSleepMinutes = (int)SleepSlider.Value;
        _config.VoiceEnabled = VoiceCheckBox.IsChecked == true;
        _config.AnimationPaused = false;

        _onSave(_config);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
