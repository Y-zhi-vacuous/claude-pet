namespace ClaudePet.Voice;

public interface IWakeWordDetector : IDisposable
{
    event Action? WakeWordDetected;
    void Start();
    void Stop();
}
