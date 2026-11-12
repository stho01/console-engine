using System;
using System.IO;
using ConsoleEngine.Ecs;
using EternalDigger.Components;
using EternalDigger.Mining;
using EternalDigger.Saving;
using EternalDigger.Systems;
using NUnit.Framework;

namespace EternalDigger.Tests;

public class SaveSystemTests
{
    private const int Seed = 42;
    private const float FrameSeconds = 1f / 60f;

    private string _directory;
    private World _world;
    private SystemGroup _simulation;
    private GameSaver _saver;
    private Entity _miner;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "EternalDiggerTests", Guid.NewGuid().ToString("N"));
        _world = new World();
        _saver = new GameSaver(Path.Combine(_directory, "save.dat"), Seed, new Mine(Seed), new Elevator());
        _simulation = new SystemGroup(_world);
        _simulation.Add(new SaveSystem(_saver));
        _miner = _world.Create(new Miner { Column = 5, Row = Layout.GroundRow, FacingX = 1 });
    }

    [TearDown]
    public void TearDown()
    {
        _world.Dispose();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Test]
    public void Update_Requested_SavesThatFrame()
    {
        _saver.Request();

        _simulation.Update(FrameSeconds);

        Assert.That(_saver.Saves, Is.EqualTo(1));
        Assert.That(_saver.IsRequested, Is.False);
        Assert.That(File.Exists(_saver.Path), Is.True);
    }

    [Test]
    public void Update_OnTheSurface_NeverAutosaves()
    {
        Play(GameSaver.AutosaveInterval * 2f);

        Assert.That(_saver.Saves, Is.Zero);
    }

    [Test]
    public void Update_Underground_AutosavesOncePerInterval()
    {
        _world.Get<Miner>(_miner).Row = Mine.SurfaceRow + 20;

        Play(GameSaver.AutosaveInterval * 2.5f);

        Assert.That(_saver.Saves, Is.EqualTo(2));
    }

    private void Play(float seconds)
    {
        for (var t = 0f; t < seconds; t += FrameSeconds)
            _simulation.Update(FrameSeconds);
    }
}
