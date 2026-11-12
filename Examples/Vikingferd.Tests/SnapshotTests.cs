using System;
using System.IO;
using System.Text;
using ConsoleEngine.Ecs;
using NUnit.Framework;
using Vikingferd.Components;
using Vikingferd.Puzzle;
using Vikingferd.Rendering;
using Vikingferd.Scenes;
using Vikingferd.Systems;

namespace Vikingferd.Tests;

/// <summary>
/// Renders the title and every level with the real systems into a <see cref="Canvas"/> and checks a few things on
/// it. With the environment variable <c>VIKINGFERD_SNAPSHOTS</c> set to a folder, each frame is also written there as
/// a PNG (<see cref="SnapshotImage"/>), so the art can be looked at without a console.
/// </summary>
public class SnapshotTests
{
    private const float FrameSeconds = 1f / 60f;

    private static int[] LevelIndices => [0, 1, 2, 3, 4];

    [Test]
    public void Title_DrawsLogoHeroesAndLevels()
    {
        var canvas = new Canvas(Layout.ScreenWidth, Layout.ScreenHeight);
        var backdrop = new Backdrop(canvas.Width, canvas.PixelHeight);
        backdrop.Update(1f);

        TitleScene.Draw(canvas, backdrop, selected: 0);

        Assert.That(Text(canvas, Layout.KeyHelpRow), Does.Contain(TitleScene.KeyHelp));
        Assert.That(Text(canvas, 28), Does.Contain(Levels.All[0].Title));
        Save(canvas, "title");
    }

    [TestCaseSource(nameof(LevelIndices))]
    public void Level_DrawsHeroesStageAndHud(int levelIndex)
    {
        using var level = new RenderedLevel(levelIndex);
        level.Expedition.Input = new PartyInput { Horizontal = 1 };
        level.Play(frames: 20);
        level.Expedition.Input = new PartyInput { Horizontal = 1, JumpPressed = true, JumpHeld = true };
        level.Play(frames: 1);
        level.Expedition.Input = new PartyInput { Horizontal = 1, JumpHeld = true };
        level.Play(frames: 12);

        Assert.That(Text(level.Canvas, Layout.HeaderRow), Does.Contain(Levels.All[levelIndex].Title));
        Assert.That(Text(level.Canvas, Layout.PanelRow), Does.Contain("YLVA"));
        Assert.That(level.CountPixels(ConsoleColor.Green), Is.GreaterThan(5), "Ylva's tunic");
        Save(level.Canvas, $"level{levelIndex + 1}");
    }

    [Test]
    public void Smash_SpawnsDebrisParticles()
    {
        using var level = new RenderedLevel(1);
        level.Expedition.Input = new PartyInput { SelectHero = 2 };
        level.Play(frames: 1);
        level.Expedition.Input = new PartyInput { Horizontal = 1 };
        level.Play(frames: 90);
        level.Expedition.Input = new PartyInput { UsePressed = true };
        level.Play(frames: 1);
        level.Expedition.Input = new PartyInput();
        level.Play(frames: 6);

        Assert.That(level.Expedition.Stage.KindAt(10, 10), Is.EqualTo(TileKind.Air), "smashed");
        Assert.That(level.ParticleCount, Is.GreaterThan(5));
        Save(level.Canvas, "smash");
    }

    [Test]
    public void Failed_ShowsTheMessageBox()
    {
        using var level = new RenderedLevel(2);
        level.Expedition.Input = new PartyInput { SelectHero = 2 };
        level.Play(frames: 1);
        level.Expedition.Input = new PartyInput { Horizontal = 1 };
        level.Play(frames: 240);

        Assert.That(level.Expedition.Outcome, Is.EqualTo(Outcome.Failed));
        Assert.That(Text(level.Canvas, Layout.StageTop + (Layout.StageAreaHeight - 7) / 2 + 2),
            Does.Contain("GORM fell too far"));
        Save(level.Canvas, "failed");
    }

    //**********************************************************
    //** helpers
    //**********************************************************

    private static string Text(Canvas canvas, int row)
    {
        var text = new StringBuilder();
        for (var x = 0; x < canvas.Width; x++)
        {
            canvas.Cell(x, row, out var character, out _, out _);
            text.Append(character);
        }

        return text.ToString();
    }

    /// <summary>Writes the canvas as a PNG if <c>VIKINGFERD_SNAPSHOTS</c> names a folder.</summary>
    private static void Save(Canvas canvas, string name)
    {
        var folder = Environment.GetEnvironmentVariable("VIKINGFERD_SNAPSHOTS");
        if (string.IsNullOrEmpty(folder))
            return;

        Directory.CreateDirectory(folder);
        SnapshotImage.Save(canvas, Path.Combine(folder, name + ".png"));
    }

    /// <summary>A level wired like <c>LevelScene</c>, drawing into a canvas instead of the console.</summary>
    private sealed class RenderedLevel : IDisposable
    {
        private readonly World _world = new();
        private readonly SystemGroup _fixed;
        private readonly SystemGroup _simulation;
        private readonly SystemGroup _presentation;
        private readonly Query _particles;

        public RenderedLevel(int levelIndex)
        {
            var stage = Stage.Parse(Levels.All[levelIndex].Rows, out var spawns);
            Expedition = new Expedition(levelIndex, stage);
            Canvas = new Canvas(Layout.ScreenWidth, Layout.ScreenHeight);
            foreach (var spawn in spawns)
                _world.Create(Heroes.Create(spawn));

            _fixed = new SystemGroup(_world);
            _fixed.Add(new PuzzleSystem(Expedition));
            _simulation = new SystemGroup(_world);
            _simulation.Add(new PartyControlSystem(Expedition));
            _simulation.Add(new EffectSystem(Expedition, new Random(1)));
            _simulation.Add(new ParticleSystem(stage));
            _presentation = new SystemGroup(_world);
            _presentation.Add(new BackdropRenderSystem(Canvas, new Backdrop(Canvas.Width, Canvas.PixelHeight)));
            _presentation.Add(new StageRenderSystem(Canvas, stage));
            _presentation.Add(new ActorRenderSystem(Canvas, Expedition));
            _presentation.Add(new ParticleRenderSystem(Canvas, Layout.StagePixelOrigin(stage.Columns, stage.Rows)));
            _presentation.Add(new HudSystem(Canvas, Expedition));
            _particles = _world.Query(Query.With<Particle>());
        }

        public Expedition Expedition { get; }
        public Canvas Canvas { get; }

        public int ParticleCount
        {
            get
            {
                var count = 0;
                foreach (var chunk in _particles.GetChunks())
                    count += chunk.Count;
                return count;
            }
        }

        /// <summary>Two fixed steps per 60 Hz frame, like the 120 Hz scene.</summary>
        public void Play(int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                _fixed.Update(FrameSeconds / 2f);
                _fixed.Update(FrameSeconds / 2f);
                _simulation.Update(FrameSeconds);
                _presentation.Update(FrameSeconds);
            }
        }

        public int CountPixels(ConsoleColor color)
        {
            var count = 0;
            for (var y = 0; y < Canvas.PixelHeight; y++)
            for (var x = 0; x < Canvas.Width; x++)
            {
                if (Canvas.PixelAt(x, y) == color)
                    count++;
            }

            return count;
        }

        public void Dispose() => _world.Dispose();
    }
}
