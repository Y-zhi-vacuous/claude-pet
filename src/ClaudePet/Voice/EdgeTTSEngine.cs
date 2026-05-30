using System.Net.Http;
using System.Text;
using NAudio.Wave;

namespace ClaudePet.Voice;

/// <summary>
/// Edge TTS（免费）——蠢萌小狗音色。
/// 用 zh-CN-XiaoxiaoNeural + 调高 pitch + 调慢 rate 实现萌感。
/// </summary>
public class EdgeTTSEngine : ITTSEngine
{
    private readonly HttpClient _http = new();
    private bool _disposed;

    // XiaoxiaoNeural: 活泼少女音，调参后变萌
    private const string VoiceName = "zh-CN-XiaoxiaoNeural";

    public async Task SpeakAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // 预处理：去掉 markdown 标记让朗读更自然
        var cleanText = text
            .Replace("```", "")
            .Replace("**", "")
            .Replace("##", "")
            .Replace("###", "")
            .Replace("汪～", "汪～")
            .Replace("嗷呜～", "嗷呜～");

        try
        {
            var ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='zh-CN'>
                <voice name='{VoiceName}'>
                    <prosody rate='-10%' pitch='+15%'>
                        {System.Net.WebUtility.HtmlEncode(cleanText)}
                    </prosody>
                </voice></speak>";

            var content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");
            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1" +
                "?TrustedClientToken=6A5AA1D4EAFF4E9FB37E23D68491D6F4")
            { Content = content };

            request.Headers.Add("User-Agent", "Mozilla/5.0");
            request.Headers.Add("Accept", "*/*");
            request.Headers.Add("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");

            var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var audioStream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new Mp3FileReader(audioStream);
            using var waveOut = new WaveOutEvent();
            waveOut.Init(reader);
            waveOut.Play();

            while (waveOut.PlaybackState == PlaybackState.Playing)
                await Task.Delay(100, ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"EdgeTTS error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _http.Dispose();
    }
}
