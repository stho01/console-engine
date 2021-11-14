using System.Linq;
using NSubstitute;
using NUnit.Framework;
using TerraForM;
using TerraForM.Commands;
using TerraForM.GameObjects;
using TerraForM.GameObjects.Tiles;

namespace TerraForM.Tests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Rover_PowerIsDepletedWhenRemainingPowerIsZeroOrLess()
        {
            var game = Substitute.For<TerraformGame>();
            var world = Substitute.For<World>();
            world.MaxPower = 10000;
            world.Sequences = 30;
            game.World = world;
            
            var sut = new Rover(game);
            sut.RemainingPower -= world.MaxPower;
            Assert.That(sut.PowerDepleted(), Is.True);
        }
    }
}

