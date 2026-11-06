using ConsoleEngine.Ecs;
using TerraForM.Ecs.Commands;

namespace TerraForM.Ecs;

/// <summary>Per-level state the systems of a <see cref="Scenes.GameScene"/> share, besides the entities.</summary>
public sealed class Level(string mapName, string name)
{
    /// <summary>Map file name without extension (<c>map1</c>). <c>R</c> reloads it.</summary>
    public string MapName { get; } = mapName;

    /// <summary>The map's <c>!name</c>, shown after <c>NOW ENTERING</c>.</summary>
    public string Name { get; } = name;

    public Entity Rover { get; set; }

    /// <summary>
    /// Recomputed at the start of every frame: out of power or more commits than sequences. Only changes the HUD.
    /// </summary>
    public bool GameOver { get; set; }

    public CommandQueue CommandQueue { get; } = new();
}
