using System;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Inputs;
using Kartster.Components;

namespace Kartster.PowerUps;

/// <summary>
/// The player's use keys, once a frame, edge-detected so one press spends one charge (Kartster's
/// <c>KeyboardKartController</c>): <see cref="UseKey"/> the power-up's own way, <see cref="ThrowKey"/> ahead,
/// <see cref="DropKey"/> behind. Kartster drops on G, which the port's game keeps for the GPU switch, so B drops. The
/// press waits in the <see cref="PowerUpField"/> for the next fixed step.
/// </summary>
/// <param name="isPressed">Whether a key went down this frame; null reads <c>Input.Instance</c>.</param>
public sealed class PowerUpInputSystem(PowerUpField field, Func<Key, bool> isPressed = null) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const Key UseKey = Key.E;
    public const Key ThrowKey = Key.T;
    public const Key DropKey = Key.B;

    private readonly Func<Key, bool> _isPressed = isPressed ?? (key => Input.Instance.GetKey(key).Pressed);
    private Query _players;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _players = World.Query(Query.With<PlayerKart, Kart>());

    protected override void OnUpdate(float deltaSeconds)
    {
        PowerUpAim aim;
        if (_isPressed(ThrowKey))
            aim = PowerUpAim.Forward;
        else if (_isPressed(DropKey))
            aim = PowerUpAim.Backward;
        else if (_isPressed(UseKey))
            aim = PowerUpAim.Default;
        else
            return;

        foreach (var chunk in _players.GetChunks())
        {
            var karts = chunk.GetSpan<Kart>();
            for (var i = 0; i < chunk.Count; i++)
                field.RequestUse(karts[i].Index, aim);
        }
    }
}
