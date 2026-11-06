using System;
using ConsoleEngine.Infrastructure.Rendering;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Infrastructure.Objects;

/// <summary>
/// A game object in the engine's format: position and appearance are typed fields the engine owns and draws, and an
/// optional <see cref="IBehaviour"/> decides what the object does. Call <see cref="Update()"/> and
/// <see cref="Render"/> from the game's or scene's <c>OnUpdate</c>/<c>OnRender</c>.
/// </summary>
public class GameObject
{
    //**********************************************************
    //** fields:
    //**********************************************************

    private IBehaviour _behaviour;
    private bool _started;

    //**********************************************************
    //** props:
    //**********************************************************

    /// <summary>Top-left cell, floored when drawn.</summary>
    public Vector2 Position { get; set; }

    /// <summary>Drawn when <see cref="Sprite"/> is <c>null</c>.</summary>
    public char Char { get; set; } = '?';
    public ConsoleColor Color { get; set; } = ConsoleColor.White;
    public ConsoleColor Background { get; set; } = ConsoleColor.Black;

    /// <summary>Drawn instead of <see cref="Char"/> when set.</summary>
    public Sprite Sprite { get; set; }

    /// <summary>Hidden objects are still updated but neither drawn nor rendered by their behaviour.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Setting a new behaviour makes it start on the next <see cref="Update()"/>.</summary>
    public IBehaviour Behaviour
    {
        get => _behaviour;
        set
        {
            _behaviour = value;
            _started = false;
        }
    }

    //**********************************************************
    //** public methods:
    //**********************************************************

    /// <summary>Updates the behaviour with <see cref="GameTime.Delta"/>.</summary>
    public void Update() => Update(GameTime.Delta);

    public void Update(TimeSpan delta)
    {
        if (_behaviour == null)
            return;

        if (!_started)
        {
            _started = true;
            _behaviour.Start(this);
        }

        _behaviour.Update(this, delta);
    }

    /// <summary>Draws <see cref="Sprite"/> or <see cref="Char"/> at <see cref="Position"/>, then lets the behaviour
    /// draw.</summary>
    public void Render(RenderConsole console)
    {
        if (!Visible)
            return;

        var x = (int)MathF.Floor(Position.X);
        var y = (int)MathF.Floor(Position.Y);
        if (Sprite != null)
            console.Draw(x, y, Sprite);
        else
            console.Draw(x, y, Char, Color, Background);

        _behaviour?.Render(this, console);
    }
}
