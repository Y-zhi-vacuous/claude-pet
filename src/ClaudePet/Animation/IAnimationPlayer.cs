using ClaudePet.Models;

namespace ClaudePet.Animation;

public interface IAnimationPlayer
{
    void Play(AnimationState state);
    void Stop();
    void SetRenderPosition(double x, double y);
    (int Width, int Height) GetSize();
    event Action<AnimationState>? AnimationEnded;
}
