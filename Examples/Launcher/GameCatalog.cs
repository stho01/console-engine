namespace Launcher;

/// <summary>
/// The games in the menu, in menu order. Each one also needs a build-order <c>ProjectReference</c> in
/// <c>Launcher.csproj</c>, so building the launcher builds it.
/// </summary>
internal static class GameCatalog
{
    public static readonly GameEntry[] Games =
    [
        new("Snake (ECS)", "Snake.Ecs", "W A S D turn"),
        new("Asteroids (ECS)", "Asteroids.Ecs", "A D steer, W thrust, Space fire"),
        new("Platformer (ECS)", "Platformer.Ecs", "A D move, Space jump, F1 debug"),
        new("TerraForM (ECS)", "TerraForM.Ecs", "Arrows plan, P plant, Space send, R restart"),
        new("ConsoleGame (ECS)", "ConsoleGame.Ecs", "Esc quit"),
        new("Swarm", "Swarm", "Space / Backspace +/- 10 000 particles, Esc quit"),
        new("Physics Sandbox", "PhysicsSandbox", "Space ball, B box, Enter 50 balls, R clear, Esc quit"),
        new("Physics Showcase", "PhysicsShowcase",
            "1-5 shapes, Enter shower, A D W move, E blast, G gravity, I smooth, R clear"),
        new("EternalDigger", "EternalDigger", "Arrows walk/dig, Space ladder, E deliver/buy, hold R rescue"),
        new("Scripted (Lua)", "Scripted", "Arrows move, F5 reload scripts, Esc quit"),
        new("Kartster (3D)", "Kartster", "W gas, S brake, A D steer, Space handbrake, R restart, Enter garage, Tab kart, G GPU")
    ];
}
