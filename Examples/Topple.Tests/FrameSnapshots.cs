using System;
using System.IO;
using System.Reflection;
using System.Text;
using ConsoleEngine.Abstractions.Rendering;
using ConsoleEngine.Infrastructure.Rendering;
using Microsoft.Xna.Framework;
using NUnit.Framework;
using Topple.Components;
using Topple.Levels;

namespace Topple.Tests;

/// <summary>
/// Not a test: renders frames of the game headless and writes them as HTML pages (square cells, the classic console
/// palette), to look at the graphics without a Windows console. Explicit, so it only runs when asked for:
/// <c>dotnet test Examples/Topple.Tests --filter FullyQualifiedName~FrameSnapshots</c>. Pages go to the folder in the
/// <c>TOPPLE_SNAPSHOTS</c> environment variable, or the test output folder.
/// </summary>
[Explicit("Writes HTML snapshots of frames; run by hand.")]
public class FrameSnapshots
{
    /// <summary>The legacy Windows console colours, in <see cref="ConsoleColor"/> order.</summary>
    private static readonly string[] Palette =
    [
        "#000000", "#000080", "#008000", "#008080", "#800000", "#800080", "#808000", "#c0c0c0",
        "#808080", "#0000ff", "#00ff00", "#00ffff", "#ff0000", "#ff00ff", "#ffff00", "#ffffff"
    ];

    private static readonly FieldInfo PixelsField =
        typeof(RenderConsole).GetField("_pixels", BindingFlags.Instance | BindingFlags.NonPublic);

    [Test]
    public void EveryLevel_AtStart()
    {
        for (var level = 0; level < LevelBook.Levels.Length; level++)
        {
            using var simulation = new LevelSimulation(LevelBook.Levels[level]);
            simulation.Run(1.5f);
            simulation.Setup.Catapult.ChargeUp(.7f);
            simulation.Render();
            Save(simulation.Console, $"level{level + 1}-start");
        }
    }

    [Test]
    public void Lookout_StoneInFlightAndHit()
    {
        using var simulation = new LevelSimulation(LevelBook.Levels[0]);
        simulation.Run(1.2f);
        var catapult = simulation.Setup.Catapult;
        catapult.ChargeUp(.75f);
        simulation.Throw(ShotKind.Stone, catapult.Muzzle, catapult.Release(.6f));

        simulation.Run(.45f);
        simulation.Render();
        Save(simulation.Console, "level1-flight");

        simulation.Run(1.1f);
        simulation.Render();
        Save(simulation.Console, "level1-after-hit");
    }

    [Test]
    public void GlassHouses_BombBlast()
    {
        using var simulation = new LevelSimulation(LevelBook.Levels[1]);
        simulation.Run(1.2f);
        simulation.Throw(ShotKind.Bomb, new Vector2(60f, 40f), new Vector2(12f, 0f));

        for (var i = 0; i < 120 && simulation.Count<Burst>() == 0; i++)
            simulation.Run(LevelSimulation.Step);
        simulation.Run(.08f);
        simulation.Render();
        Save(simulation.Console, "level2-blast");

        simulation.Run(.6f);
        simulation.Render();
        Save(simulation.Console, "level2-after-blast");
    }

    [Test]
    public void StoneKeep_DamagedAndTilted()
    {
        using var simulation = new LevelSimulation(LevelBook.Levels[2]);
        simulation.Run(1.2f);
        simulation.Throw(ShotKind.Stone, new Vector2(60f, 30f), new Vector2(40f, -2f));
        simulation.Run(.8f);
        simulation.Render();
        Save(simulation.Console, "level3-hit");
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static void Save(RenderConsole console, string name)
    {
        var pixels = (Pixel[])PixelsField.GetValue(console);
        var html = new StringBuilder();
        html.Append("<!doctype html><meta charset=\"utf-8\"><title>").Append(name).Append("</title><style>")
            .Append("body{background:#111;margin:16px}.f{display:grid;grid-template-columns:repeat(")
            .Append(console.Width).Append(",10px);grid-auto-rows:10px;font:10px/10px Consolas,monospace}")
            .Append(".f span{display:block;text-align:center;overflow:hidden;white-space:pre}</style><div class=\"f\">");
        foreach (var pixel in pixels)
        {
            html.Append("<span style=\"color:").Append(Palette[(int)pixel.ForegroundColor])
                .Append(";background:").Append(Palette[(int)pixel.BackgroundColor]).Append("\">")
                .Append(Escape(pixel.Char)).Append("</span>");
        }

        html.Append("</div>");
        var folder = Environment.GetEnvironmentVariable("TOPPLE_SNAPSHOTS") ?? TestContext.CurrentContext.WorkDirectory;
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name + ".html");
        File.WriteAllText(path, html.ToString());
        File.WriteAllText(Path.Combine(folder, name + ".frame"), Frame(pixels, console.Width, console.Height));
        TestContext.Out.WriteLine(path);
    }

    /// <summary>
    /// The frame as text, for tools that draw it as an image: the size, then one line of characters per row, one line
    /// of foreground colours (hex digits) per row and one line of background colours per row.
    /// </summary>
    private static string Frame(Pixel[] pixels, int width, int height)
    {
        var text = new StringBuilder().Append(width).Append(' ').Append(height).Append('\n');
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                text.Append(pixels[y * width + x].Char is '\0' ? ' ' : pixels[y * width + x].Char);
            text.Append('\n');
        }

        foreach (var background in new[] { false, true })
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var pixel = pixels[y * width + x];
                    text.Append(((int)(background ? pixel.BackgroundColor : pixel.ForegroundColor)).ToString("x"));
                }

                text.Append('\n');
            }
        }

        return text.ToString();
    }

    private static string Escape(char character) => character switch
    {
        '<' => "&lt;",
        '>' => "&gt;",
        '&' => "&amp;",
        '\0' => " ",
        _ => character.ToString()
    };
}
