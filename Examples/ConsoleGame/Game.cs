using System;
using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Abstractions.Rendering;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Inputs;

namespace ConsoleGame;

public class Game() : GameBase(Width, Height, fontWidth: 8, fontHeight: 8)
{
    private static readonly char[] Chars = { '*', '#', 'A', '?', 'H' };
    private static readonly ConsoleColor[] Colors = { ConsoleColor.Cyan, ConsoleColor.Red, ConsoleColor.Green, ConsoleColor.White, ConsoleColor.DarkGray };
    private static readonly Random Random = new(DateTime.Now.Millisecond);
    private const int Width = 64;
    private const int Height = 64;
    private readonly Pixel[,] _display = new Pixel[Width,Height];

    protected override void OnInitialize()
    {
        // callback called every 500ms
        _ = GameTime.SetInterval(500, () => {
            for (var x = 0; x < Width; x++)
            for (var y = 0; y < Height; y++) {
                _display[x, y].Char = '█'; //Chars[Random.Next(0, Chars.Length)];
                _display[x, y].ForegroundColor = Colors[Random.Next(0, Colors.Length)];
            }
        });
    }

    protected override void OnUpdate()
    {
        if (Input.Instance.GetKey(Key.ESCAPE).Pressed) 
            Stop();
    }

    protected override void OnRender()
    {
        Console.Draw(0,0, _display);
    }
}