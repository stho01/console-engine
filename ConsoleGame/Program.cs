using System;
using System.Linq;
using ConsoleEngine;
using ConsoleEngine.Infrastructure;

namespace ConsoleGame
{
    class Program
    {
        static void Main(string[] args)
        {
            var game = new Game {
                ShowFps = true
            };
            game.Initialize();
            game.Start();
            
            // var dim = 40;
            // var input = new Input();
            //
            // var window = new RenderWindow(dim, dim) {
            //     Resizeable = false,
            //     HideCursor = true,
            //     FontWidth = 20,
            //     FontHeight = 20
            // }.Initialize();
            //
            // while (true)
            // {
            //     window.Clear();
            //     Timer.Update();
            //     input.Update();
            //
            //
            //     
            //     window.SetTitle($"FPS: {Timer.Fps}");
            //     window.Display();
            // }
        }
    }
}