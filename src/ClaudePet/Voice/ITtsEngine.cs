namespace ClaudePet.Voice;

public interface ITTSEngine : IDisposable
{
    /// <summary>将文字合成为语音并播放</summary>
    Task SpeakAsync(string text, CancellationToken ct = default);
}
