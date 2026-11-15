using System;
using ConsoleEngine.Ecs;
using Starguard.Scenes;

namespace Starguard.Systems;

/// <summary>After game over: records the high score, and ENTER starts a new match in a new scene.</summary>
public sealed class RestartSystem(StarguardGame game, Match match, Controls controls) : EntitySystem
{
    protected override void OnUpdate(float deltaSeconds)
    {
        if (!match.IsOver)
            return;

        game.HighScore = Math.Max(game.HighScore, match.Score);
        if (controls.Restart)
            game.Scenes.Set(new BattleScene());
    }
}
