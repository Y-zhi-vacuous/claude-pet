using System.Text;

namespace ClaudePet.Utils;

/// <summary>
/// 聊天记录保存到 chat_history.md。
/// </summary>
public static class ChatLogger
{
    private static readonly string LogPath = @"D:\dev\claude-pet\chat_history.md";
    private static readonly object _lock = new();

    public static void LogUser(string text)
    {
        Append($"**用户** ({DateTime.Now:HH:mm}):\n\n{text}\n\n");
    }

    public static void LogReply(string text)
    {
        Append($"**千千** ({DateTime.Now:HH:mm}):\n\n{text}\n\n---\n\n");
    }

    private static void Append(string text)
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(LogPath))
                    File.WriteAllText(LogPath, "# 千千聊天记录\n\n> {DateTime.Now:yyyy-MM-dd}\n\n---\n\n", Encoding.UTF8);
                File.AppendAllText(LogPath, text, Encoding.UTF8);
            }
            catch { }
        }
    }
}
