using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Infrastructure.Rendering;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using EternalDigger.Rendering;
using EternalDigger.Systems;
using EternalDigger.Tests.Fakes;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace EternalDigger.Tests;

/// <summary>
/// Digs the toughest tile frame by frame with the real particle systems and renderers, and reads the frame buffer
/// back after every frame: the miner's face and lamp must always be on screen, and no particle on his cells.
/// </summary>
public class MinerVisibilityTests
{
    private const float FrameSeconds = 1f / 60f;
    private const int Seed = 42;
    private const int Column = 10;
    private const int Row = Mine.SurfaceRow + 100;

    private World _world;
    private Mine _mine;
    private Camera _camera;
    private RenderConsole _console;
    private SystemGroup _simulation;
    private SystemGroup _presentation;
    private MinerControlSystem _control;
    private MinerMotionSystem _motion;
    private EffectSpawner _effects;
    private Entity _miner;

    [SetUp]
    public void SetUp()
    {
        _world = new World();
        _mine = new Mine(Seed);
        _camera = new Camera { Position = new Vector2(0f, Row * Layout.TileHeight - 20) };
        _console = new RenderConsole(new FakeConsoleHandler(Layout.ScreenWidth, Layout.ScreenHeight));
        var canvas = new Canvas(_console);
        var light = new LightMap(Mine.Columns, Layout.ScreenHeight / Layout.TileHeight + 2);
        var random = new Random(Seed);
        var effects = new EffectSpawner(random, new ScreenShake());
        _effects = effects;
        _control = new MinerControlSystem(_mine, new Elevator(), effects);
        _motion = new MinerMotionSystem(_mine, effects);

        _simulation = new SystemGroup(_world);
        _simulation.Add(new LightingSystem(_mine, light, _camera));
        _simulation.Add(new DynamiteSystem(_mine, effects));
        _simulation.Add(new ParticlePhysicsSystem(_mine));
        _simulation.Add(new ParticleLifetimeSystem());

        _presentation = new SystemGroup(_world);
        _presentation.Add(new ParticleRenderSystem(canvas, _camera, light));
        _presentation.Add(new MinerRenderSystem(canvas, _camera));

        _mine.At(Column, Row) = new TileCell { Type = TileType.Air };
        _mine.At(Column, Row + 1) = new TileCell { Type = TileType.Granite };
        _miner = _world.Create(
            new Position(Layout.TileToWorld(Column, Row)),
            new Miner
            {
                Column = Column,
                Row = Row,
                MoveFrom = new Vector2(Column, Row),
                FacingX = 1,
                LampDirection = Vector2.UnitX
            });
    }

    [TearDown]
    public void TearDown() => _world.Dispose();

    [TestCase(1, 0)]
    [TestCase(-1, 0)]
    [TestCase(0, 1)]
    public void Render_HoldingDigOnGold_ShowsFaceAndLampEveryFrame(int dx, int dy)
    {
        // Gold is the toughest tile, and toughness grows with depth: 12 hits at 100 m.
        _mine.At(Column + dx, Row + dy) = new TileCell { Type = TileType.Gold };
        var input = new MinerInput { Horizontal = dx, Down = dy > 0 };
        var mostParticles = 0;

        for (var frame = 0; frame < (int)(2.5f / FrameSeconds); frame++)
        {
            Step(input);
            mostParticles = Math.Max(mostParticles, CountParticles());
            AssertMinerVisible(frame);
        }

        Assert.That(_mine.TypeAt(Column + dx, Row + dy), Is.EqualTo(TileType.Air), "the gold tile broke");
        Assert.That(mostParticles, Is.GreaterThan(20), "digging made particles");
    }

    [Test]
    public void Render_BlastRightNextToTheMiner_ShowsFaceAndLampEveryFrame()
    {
        _mine.At(Column + 1, Row) = new TileCell { Type = TileType.Granite };
        _mine.At(Column - 1, Row) = new TileCell { Type = TileType.Air };
        _mine.At(Column - 1, Row + 1) = new TileCell { Type = TileType.Granite };
        _effects.PlantDynamite(_simulation.Commands, Column + 1, Row, fuse: .2f);
        _simulation.Update(0f);
        var mostParticles = 0;

        for (var frame = 0; frame < (int)(2f / FrameSeconds); frame++)
        {
            Step(default);
            mostParticles = Math.Max(mostParticles, CountParticles());
            AssertMinerVisible(frame);
        }

        Assert.That(_mine.TypeAt(Column + 1, Row), Is.EqualTo(TileType.Air), "the blast went off");
        Assert.That(mostParticles, Is.GreaterThan(200), "a blast is hundreds of particles");
    }

    [Test]
    public void Dim_BrightColors_TurnIntoTheirDarkTwin()
    {
        Assert.That(ParticleRenderSystem.Dim(ConsoleColor.White), Is.EqualTo(ConsoleColor.Gray));
        Assert.That(ParticleRenderSystem.Dim(ConsoleColor.Yellow), Is.EqualTo(ConsoleColor.DarkYellow));
        Assert.That(ParticleRenderSystem.Dim(ConsoleColor.Cyan), Is.EqualTo(ConsoleColor.DarkCyan));
        Assert.That(ParticleRenderSystem.Dim(ConsoleColor.Gray), Is.EqualTo(ConsoleColor.Gray));
        Assert.That(ParticleRenderSystem.Dim(ConsoleColor.DarkGray), Is.EqualTo(ConsoleColor.DarkGray));
    }

    private void Step(in MinerInput input)
    {
        ref var miner = ref _world.Get<Miner>(_miner);
        ref var position = ref _world.Get<Position>(_miner);
        _control.Control(ref miner, ref position, input, FrameSeconds, _simulation.Commands);
        _motion.Move(ref miner, ref position, FrameSeconds, _simulation.Commands);
        _simulation.Update(FrameSeconds);

        _console.Clear();
        _presentation.Update(FrameSeconds);
    }

    private void AssertMinerVisible(int frame)
    {
        var miner = _world.Get<Miner>(_miner);
        var position = _world.Get<Position>(_miner).Value;
        var x = (int)MathF.Floor(position.X - _camera.Position.X);
        var y = (int)MathF.Floor(position.Y - _camera.Position.Y);
        var facingRight = miner.FacingX >= 0;
        int Cell(int fromBack) => facingRight ? x + fromBack : x + Layout.TileWidth - 1 - fromBack;

        Assert.That(_console.GetCharAt(Cell(2), y), Is.EqualTo('☻'), $"face, frame {frame}");
        Assert.That(_console.GetCharAt(Cell(3), y), Is.AnyOf('•', '▲', '▼'), $"lamp, frame {frame}");
        // The back column is not part of the sprite: a particle there would show.
        Assert.That(_console.GetCharAt(Cell(0), y), Is.EqualTo(' '), $"back, top, frame {frame}");
        Assert.That(_console.GetCharAt(Cell(0), y + 1), Is.EqualTo(' '), $"back, bottom, frame {frame}");
    }

    private int CountParticles()
    {
        var count = 0;
        foreach (var chunk in _world.Query(Query.With<Particle>()).GetChunks())
            count += chunk.Count;

        return count;
    }
}
