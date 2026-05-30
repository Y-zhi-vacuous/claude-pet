# P3: 外观阶段 — 精灵替换 + 聊天气泡 实现计划

> **Goal:** 小狗从彩色方块变成真正的比熊外观，能看到聊天对话气泡，能从气泡发消息给 Claude。

**Architecture:** SpriteGenerator 用 System.Drawing 程序化生成小狗 PNG 帧（白色绒毛球 + 耳朵 + 眼睛 + 尾巴），SpriteSheetPlayer 改为 Image 控件显示 PNG。BubbleOverlay 是 WPF UserControl，浮在小狗上方，含对话历史和输入框，输入框发送到 ClaudeBridge。

**Tech Stack:** C# 12, WPF, System.Drawing, Newtonsoft.Json

**Prerequisite:** P1 + P2 已完成

---

## 文件变更

```
新增:
  src/ClaudePet/Animation/SpriteGenerator.cs   # 程序化生成比熊精灵PNG帧
  src/ClaudePet/UI/BubbleOverlay.xaml          # 聊天气泡UI
  src/ClaudePet/UI/BubbleOverlay.xaml.cs       # 气泡逻辑

修改:
  src/ClaudePet/Animation/SpriteSheetPlayer.cs  # 彩色方块 → Image+PNG渲染
  src/ClaudePet/Animation/AnimationCatalog.cs   # 指向实际PNG文件路径
  src/ClaudePet/UI/PetWindow.xaml               # + BubbleOverlay
  src/ClaudePet/UI/PetWindow.xaml.cs            # 点击小狗弹出/收起气泡逻辑

创建目录:
  assets/sprites/idle/  等11个动画文件夹
```

---

### Task 1: 创建 SpriteGenerator（程序化比熊精灵生成）

创建 `D:\dev\claude-pet\src\ClaudePet\Animation\SpriteGenerator.cs`

用 System.Drawing 画简单的比熊小狗：
- 白色椭圆身体（大团绒毛）
- 白色椭圆头部（稍小）
- 黑色圆点眼睛 ×2
- 黑色三角鼻子
- 棕色垂耳朵 ×2（椭圆）
- 小尾巴（不同帧不同角度 = 摇摆效果）
- 短腿 ×4（小椭圆）

每个 AnimationState 生成 4-8 帧 PNG，保存到 assets/sprites/<state>/frame_01.png

```csharp
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ClaudePet.Animation;

public static class SpriteGenerator
{
    private const int Size = 140;

    public static void GenerateAllFrames(string assetsDir)
    {
        Directory.CreateDirectory(assetsDir);

        foreach (AnimationState state in Enum.GetValues<AnimationState>())
        {
            var dir = Path.Combine(assetsDir, state.ToString().ToLower());
            Directory.CreateDirectory(dir);

            int frameCount = state switch
            {
                AnimationState.Idle => 6,
                AnimationState.WalkLeft or AnimationState.WalkRight
                    or AnimationState.WalkUp or AnimationState.WalkDown => 4,
                AnimationState.SitSleep => 4,
                AnimationState.Play => 6,
                AnimationState.Think => 4,
                AnimationState.Talk => 4,
                AnimationState.Happy => 6,
                AnimationState.Eat => 4,
                _ => 4
            };

            for (int i = 0; i < frameCount; i++)
            {
                var path = Path.Combine(dir, $"frame_{i + 1:D2}.png");
                if (File.Exists(path)) continue; // 已存在则跳过

                using var bmp = new Bitmap(Size, Size);
                using var g = Graphics.FromImage(bmp);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                DrawDog(g, state, i, frameCount);

                bmp.Save(path, ImageFormat.Png);
            }
        }
    }

    private static void DrawDog(Graphics g, AnimationState state, int frame, int totalFrames)
    {
        float t = (float)frame / totalFrames; // 0..1 动画进度

        // 身体 (白色绒毛椭圆)
        using var bodyBrush = new SolidBrush(Color.FromArgb(255, 252, 248));
        using var bodyPen = new Pen(Color.FromArgb(200, 180, 160), 1.5f);
        var bodyX = 30f; var bodyY = 55f; var bodyW = 80f; var bodyH = 65f;
        g.FillEllipse(bodyBrush, bodyX, bodyY, bodyW, bodyH);
        g.DrawEllipse(bodyPen, bodyX, bodyY, bodyW, bodyH);

        // 头部偏移（不同状态不同位置）
        float headOffsetX = 0, headOffsetY = 0;
        switch (state)
        {
            case AnimationState.Think: headOffsetX = -5f * (float)Math.Sin(t * Math.PI * 2); headOffsetY = -3f; break;
            case AnimationState.Happy: headOffsetY = -5f * (float)Math.Sin(t * Math.PI * 2); break;
            case AnimationState.SitSleep: headOffsetY = 8f; headOffsetX = 2f; break;
        }

        // 头部 (白色圆)
        var headX = 25f + headOffsetX; var headY = 10f + headOffsetY; var headW = 50f; var headH = 48f;
        g.FillEllipse(bodyBrush, headX, headY, headW, headH);
        g.DrawEllipse(bodyPen, headX, headY, headW, headH);

        // 耳朵 (棕色椭圆) ×2
        using var earBrush = new SolidBrush(Color.FromArgb(180, 140, 100));
        float earAngle = state == AnimationState.Happy ? (float)Math.Sin(t * Math.PI * 3) * 10f : 0;
        g.TranslateTransform(headX + 12, headY + 5);
        g.RotateTransform(-20f + earAngle);
        g.FillEllipse(earBrush, -8f, -15f, 16f, 28f);
        g.ResetTransform();
        g.TranslateTransform(headX + headW - 12, headY + 5);
        g.RotateTransform(20f - earAngle);
        g.FillEllipse(earBrush, -8f, -15f, 16f, 28f);
        g.ResetTransform();

        // 眼睛 ×2
        using var eyeBrush = new SolidBrush(Color.FromArgb(40, 30, 20));
        float eyeY = headY + 18f;
        if (state == AnimationState.SitSleep) { eyeY += 2f; } // 闭眼往下
        float blinkScale = 1f;
        if (state == AnimationState.Idle && frame % 3 == 0) blinkScale = 0.2f; // 偶尔眨眼
        float eyeW = 6f * blinkScale; float eyeH = 7f;
        g.FillEllipse(eyeBrush, headX + 14f - eyeW / 2, eyeY, eyeW, eyeH);
        g.FillEllipse(eyeBrush, headX + headW - 14f - eyeW / 2, eyeY, eyeW, eyeH);

        // 鼻子 (黑色三角)
        using var noseBrush = new SolidBrush(Color.FromArgb(30, 20, 10));
        var noseX = headX + headW / 2f; var noseY = headY + 28f;
        var nosePts = new PointF[] {
            new(noseX - 4, noseY - 2), new(noseX + 4, noseY - 2), new(noseX, noseY + 3)
        };
        g.FillPolygon(noseBrush, nosePts);

        // 嘴巴 (小弧线)
        using var mouthPen = new Pen(Color.FromArgb(120, 90, 60), 1f);
        if (state == AnimationState.Happy)
            g.DrawArc(mouthPen, noseX - 6, noseY - 2, 12, 10, 0, 180);
        else if (state != AnimationState.SitSleep)
            g.DrawArc(mouthPen, noseX - 4, noseY + 2, 8, 5, 0, -180);

        // 尾巴 (小椭圆，角度随帧变化)
        using var tailBrush = new SolidBrush(Color.FromArgb(250, 245, 240));
        float tailAngle = state switch
        {
            AnimationState.Happy => (float)Math.Sin(t * Math.PI * 4) * 30f,
            AnimationState.Idle => (float)Math.Sin(t * Math.PI * 2) * 15f,
            _ => 0
        };
        g.TranslateTransform(bodyX + bodyW - 5, bodyY + 15);
        g.RotateTransform(30f + tailAngle);
        g.FillEllipse(tailBrush, -5f, -12f, 10f, 18f);
        g.ResetTransform();

        // 前腿 ×2 (短椭圆，走状态时有位移)
        float legAnim = 0;
        if (state.ToString().StartsWith("Walk")) legAnim = (float)Math.Sin(t * Math.PI * 2) * 5f;
        using var legBrush = new SolidBrush(Color.FromArgb(248, 242, 235));
        g.FillEllipse(legBrush, bodyX + 15f, bodyY + bodyH - 5, 10f, 20f + legAnim);
        g.FillEllipse(legBrush, bodyX + bodyW - 25f, bodyY + bodyH - 5, 10f, 20f - legAnim);

        // 后腿 ×2
        g.FillEllipse(legBrush, bodyX + 25f, bodyY + bodyH - 8, 12f, 18f - legAnim);
        g.FillEllipse(legBrush, bodyX + bodyW - 35f, bodyY + bodyH - 8, 12f, 18f + legAnim);
    }
}
```

### Task 2: 修改 SpriteSheetPlayer 支持 PNG 渲染

将 SpriteSheetPlayer 从彩色 Rectangle 改为 Image 控件加载 PNG：

- 构造函数改为从 assets 路径加载 Image
- RenderFrame 改为 `_image.Source = new BitmapImage(new Uri(frame.ImagePath))`

### Task 3: 更新 AnimationCatalog 指向 PNG 文件

AnimationCatalog.GetFrames 改为返回 assets/sprites/<state>/frame_XX.png 路径。

### Task 4: 创建 BubbleOverlay（聊天气泡）

WPF UserControl：带圆角的聊天气泡，有对话展示区和输入框。
输入框发送消息到 PetWindow 暴露的 SendMessage 回调。<｜end▁of▁thinking｜>区域在 P2 结束时恢复为空，需要重新加载完整背景。让我先把全部上下文串起来，然后写 P3 计划并一次性执行。

P1 已有：透明窗口 + 彩色方块动画 + 托盘 + 配置 + 拖拽
P2 已有：ClaudeBridge（ProcessManager + TranscriptWatcher）+ HudMiniBar 状态条

P3 要做三件事：
1. 程序化生成比熊小狗 PNG 精灵帧（用 System.Drawing 画白色绒毛小狗）
2. SpriteSheetPlayer 改成 Image 控件渲染 PNG（替代彩色方块）
3. BubbleOverlay 聊天气泡（点击小狗弹出，可发消息给 Claude）

直接写计划 + 执行。

<｜｜DSML｜｜tool_calls>
<｜｜DSML｜｜invoke name="Write">
<｜｜DSML｜｜parameter name="content" string="true"># P3: 外观阶段 — 精灵替换 + 聊天气泡

> **Goal:** 小狗从彩色方块变成手绘风比熊外观；点击弹出聊天气泡，气泡内发消息给 Claude。

**Architecture:** SpriteGenerator 程序化绘制比熊 PNG 帧（白色绒毛体 + 垂耳 + 圆眼 + 尾巴摇摆）。SpriteSheetPlayer 从 Rectangle 改为 Image 控件加载 PNG。BubbleOverlay 浮在小狗上方，含对话列表和输入框。

---

## 文件变更

```
新增:
  src/ClaudePet/Animation/SpriteGenerator.cs    # 程序化比熊精灵PNG生成
  src/ClaudePet/UI/BubbleOverlay.xaml           # 聊天气泡
  src/ClaudePet/UI/BubbleOverlay.xaml.cs

修改:
  src/ClaudePet/Animation/SpriteSheetPlayer.cs  # Rectangle→Image+PNG
  src/ClaudePet/Animation/AnimationCatalog.cs   # 颜色→PNG文件路径
  src/ClaudePet/UI/PetWindow.xaml               # +BubbleOverlay
  src/ClaudePet/UI/PetWindow.xaml.cs            # 点击切换气泡+发送消息
  src/ClaudePet/App.xaml.cs                     # 启动时调SpriteGenerator
```

---

### Task 1: SpriteGenerator — 程序化小狗精灵

### Task 2: 改 SpriteSheetPlayer → PNG Image 渲染

### Task 3: 改 AnimationCatalog → PNG 路径

### Task 4: BubbleOverlay 聊天气泡 + PetWindow 集成
