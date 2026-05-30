using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClaudePet.Models;

namespace ClaudePet.Animation;

/// <summary>
/// 像素帧动画播放器。从 assets/sprites/ 加载 PNG 序列帧，
/// 按状态切换帧组，循环/单次播放。
/// </summary>
public class SpriteSheetPlayer : IAnimationPlayer, IDisposable
{
    private readonly Canvas _canvas;
    private readonly Image _image;
    private readonly System.Windows.Threading.DispatcherTimer _timer;
    private AnimationState _currentState;
    private List<AnimationFrame> _frames = new();
    private int _frameIndex;
    private bool _disposed;
    private bool _isLooping;

    public event Action<AnimationState>? AnimationEnded;
    public AnimationState CurrentState => _currentState;

    public SpriteSheetPlayer(Canvas canvas)
    {
        _canvas = canvas;

        _image = new Image
        {
            Width = 140,
            Height = 140,
            Stretch = Stretch.Uniform
        };
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);

        Canvas.SetLeft(_image, (200 - 140) / 2);
        Canvas.SetTop(_image, (200 - 140) / 2);
        _canvas.Children.Add(_image);

        _timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _timer.Tick += OnTick;

        Play(AnimationState.Idle);
    }

    public void Play(AnimationState state)
    {
        if (_currentState == state && _frames.Count > 0) return;
        _currentState = state;
        _frameIndex = 0;

        _frames = AnimationCatalog.GetFrames(state);
        _isLooping = IsLooping(state);

        if (_frames.Count > 0) RenderFrame();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();
    public void SetRenderPosition(double x, double y) { }
    public (int Width, int Height) GetSize() => (140, 140);

    private void OnTick(object? sender, EventArgs e)
    {
        if (_disposed || _frames.Count == 0) return;
        _frameIndex++;
        if (_frameIndex >= _frames.Count)
        {
            if (_isLooping) { _frameIndex = 0; }
            else { _timer.Stop(); AnimationEnded?.Invoke(_currentState); return; }
        }
        RenderFrame();
    }

    private void RenderFrame()
    {
        if (_frameIndex >= _frames.Count) return;
        var frame = _frames[_frameIndex];
        try
        {
            if (File.Exists(frame.ImagePath))
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(frame.ImagePath, UriKind.Absolute);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                _image.Source = bi;
            }
        }
        catch { /* 保持上一帧 */ }
    }

    private static bool IsLooping(AnimationState state) => state switch
    {
        AnimationState.Idle or AnimationState.WalkLeft or AnimationState.WalkRight
            or AnimationState.WalkUp or AnimationState.WalkDown => true,
        _ => false
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _canvas.Children.Remove(_image);
    }
}
