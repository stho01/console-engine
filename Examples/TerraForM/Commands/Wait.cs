using TerraForM.GameObjects;

namespace TerraForM.Commands;

public class Wait : Command
{
    public Wait(float durationInMilliseconds) : base(durationInMilliseconds)
    {
    }

    protected override void OnUpdate(Rover rover)
    {
        // Just wait
    }

    public override char GetVisualRepresentation()
    {
        return 'W';
    }
}