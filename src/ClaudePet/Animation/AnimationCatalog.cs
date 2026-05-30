using ClaudePet.Models;

namespace ClaudePet.Animation;

/// <summary>
/// 精灵帧目录。从 assets/sprites/<state>/frame_*.png 读取帧序列。
/// 没有 PNG 时返回占位帧。
/// </summary>
public static class AnimationCatalog
{
    private static string? _assetsDir;

    public static string? AssetsDir => _assetsDir;

    public static void SetAssetsDirectory(string dir)
    {
        _assetsDir = dir;
    }

    public static List<AnimationFrame> GetFrames(AnimationState state)
    {
        var frames = new List<AnimationFrame>();

        if (_assetsDir != null)
        {
            var dir = Path.Combine(_assetsDir, state.ToString().ToLower());
            if (Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir, "frame_*.png")
                    .OrderBy(f => f)
                    .ToList();

                if (files.Count > 0)
                {
                    foreach (var f in files)
                        frames.Add(new AnimationFrame { ImagePath = f, DurationMs = 150 });
                    return frames;
                }
            }
        }

        // 没有任何 PNG 帧 → 返回单个占位帧（用户图片）
        var userImg = @"D:\dev\claude-pet\Image\千千.png";
        if (File.Exists(userImg))
        {
            frames.Add(new AnimationFrame { ImagePath = userImg, DurationMs = 200 });
        }

        return frames;
    }
}
