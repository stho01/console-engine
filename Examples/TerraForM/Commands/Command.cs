using ConsoleEngine.Infrastructure;
using TerraForM.GameObjects;

namespace TerraForM.Commands;

public abstract class Command
{
        
    protected double _elapsed = 0.0f;
    public readonly float DurationInMilliseconds;

    public Command(float durationInMilliseconds)
    {
        DurationInMilliseconds = durationInMilliseconds;
    }

    protected abstract void OnUpdate(Rover rover);

    public void Update(Rover rover)
    {
        OnUpdate(rover);
        _elapsed += GameTime.Delta.TotalMilliseconds;
    }

    public bool IsDone() => _elapsed > DurationInMilliseconds;
        
    public abstract char GetVisualRepresentation();
}