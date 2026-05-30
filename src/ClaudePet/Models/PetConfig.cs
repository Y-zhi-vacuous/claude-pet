namespace ClaudePet.Models;

public class PetConfig
{
    public string PetName { get; set; } = "旺财";
    public double WindowLeft { get; set; } = -1;
    public double WindowTop { get; set; } = -1;
    public bool AnimationPaused { get; set; }
    public bool VoiceEnabled { get; set; } = false;
    public string WakeWord { get; set; } = "嘿小狗";
    public int IdleSleepMinutes { get; set; } = 5;
    public string WorkingDirectory { get; set; } = ".";
    public string TranscriptsDirectory { get; set; } = "";
}
