using System.Linq;
using NSubstitute;
using NUnit.Framework;
using TerraForM;
using TerraForM.Commands;
using TerraForM.GameObjects;
using TerraForM.GameObjects.Tiles;
using TerraForM.Scenes;

namespace TerraForM.Tests;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Rover_PowerIsDepletedWhenRemainingPowerIsZeroOrLess()
    {
        var world = Substitute.For<World>();
        var scene = Substitute.For<GameScene>("map");
            
        scene.World.Returns(world);
        world.MaxPower = 10000;
        world.Sequences = 30;
            
        var sut = new Rover(scene);
        sut.RemainingPower -= world.MaxPower;
        Assert.That(sut.PowerDepleted(), Is.True);
    }
}