using TerraForM.GameObjects;

namespace TerraForM.Commands;

public class Plant() : Command(1.0f)
{
    protected override void OnUpdate(Rover rover)
    {
        rover.Plant();
    }

    public override char GetVisualRepresentation()
    {
        return 'P';
    }
}