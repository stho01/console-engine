namespace TerraForM.Ecs.Commands;

public enum CommandKind : byte
{
    Move,
    Plant
}

/// <summary>
/// One step of a command sequence. A struct, so queueing and running commands doesn't allocate. It runs every frame
/// until the time it has been running exceeds <see cref="DurationMs"/>.
/// </summary>
public readonly struct RoverCommand
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float MoveDurationMs = 100f;
    private const float PlantDurationMs = 1f;

    public readonly CommandKind Kind;
    public readonly Direction Direction;
    public readonly float DurationMs;

    //**********************************************************
    //** ctor
    //**********************************************************

    private RoverCommand(CommandKind kind, Direction direction, float durationMs)
    {
        Kind = kind;
        Direction = direction;
        DurationMs = durationMs;
    }

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>How the HUD shows the command after <c>QUEUED COMMANDS</c>.</summary>
    public char Symbol => Kind == CommandKind.Plant
        ? 'P'
        : Direction switch
        {
            Direction.North => '↑',
            Direction.South => '↓',
            Direction.East => '→',
            _ => '←'
        };

    //**********************************************************
    //** public methods
    //**********************************************************

    public static RoverCommand Move(Direction direction) => new(CommandKind.Move, direction, MoveDurationMs);

    public static RoverCommand Plant() => new(CommandKind.Plant, default, PlantDurationMs);
}
