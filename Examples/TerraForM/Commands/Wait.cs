using TerraForM.GameObjects;

namespace TerraForM.Commands;

public class Wait(float durationInMilliseconds) : Command(durationInMilliseconds)
{
    protected override void OnUpdate(Rover rover)
    {
        // Just wait
    }

    public override char GetVisualRepresentation()
    {
        return 'W';
    }
}