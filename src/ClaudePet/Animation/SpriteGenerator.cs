namespace ClaudePet.Animation;

/// <summary>
/// 精灵帧生成器。
/// 当前关闭自动生成——所有帧由外部提供（放到 assets/sprites/ 下即可）。
/// 只负责创建目录结构。
/// </summary>
public static class SpriteGenerator
{
    /// <summary>创建精灵帧目录结构（不生成任何图片）</summary>
    public static void GenerateAllFrames(string assetsDir)
    {
        foreach (var state in System.Enum.GetValues<Models.AnimationState>())
        {
            var dir = System.IO.Path.Combine(assetsDir, state.ToString().ToLower());
            System.IO.Directory.CreateDirectory(dir);
        }
    }
}
