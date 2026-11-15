using Microsoft.Xna.Framework;
using NUnit.Framework;
using SkyStack.Climbing;
using SkyStack.Components;

namespace SkyStack.Tests;

/// <summary>Plays fixed steps of <see cref="ClimbPhysics.Step"/> with scripted input, like the motion system does.</summary>
public class ClimbPhysicsTests
{
    private const float StepSeconds = 1f / 60f;

    private Tower _tower;
    private Climber _climber;
    private Vector2 _position;
    private Vector2 _velocity;
    private int _highestLanding;

    [SetUp]
    public void SetUp()
    {
        _tower = new Tower(11);
        _climber = new Climber { IsGrounded = true };
        _position = new Vector2(20f, Tower.RowOf(0));
        _velocity = Vector2.Zero;
        _highestLanding = ClimbPhysics.NoLanding;
    }

    [Test]
    public void Step_HoldRight_RunsRightUpToMaxSpeed()
    {
        _climber.RunDirection = 1f;

        Play(seconds: .2f);

        Assert.That(_position.X, Is.GreaterThan(20f));
        Assert.That(_velocity.X, Is.GreaterThan(0f).And.LessThanOrEqualTo(ClimbPhysics.MaxRunSpeed));
        Assert.That(_climber.IsGrounded, Is.True);
    }

    [Test]
    public void Step_NoInputOnTheGround_SlowsDownToAStop()
    {
        _velocity.X = 30f;

        Play(seconds: 1f);

        Assert.That(_velocity.X, Is.Zero);
    }

    [Test]
    public void Step_RunIntoTheWall_BouncesBackWithMostOfTheSpeed()
    {
        // In the air there is no ground friction, so only the bounce changes the speed.
        _climber.IsGrounded = false;
        _position.X = ClimbPhysics.MaxX - .5f;
        _velocity.X = 50f;

        Play(seconds: StepSeconds);

        Assert.That(_velocity.X, Is.EqualTo(-50f * ClimbPhysics.WallBounce).Within(.01f));
        Assert.That(_position.X, Is.LessThanOrEqualTo(ClimbPhysics.MaxX));
        Assert.That(_climber.SparkSide, Is.EqualTo(1));
    }

    [Test]
    public void JumpSpeed_RunningFaster_JumpsHigher() =>
        Assert.That(ClimbPhysics.JumpSpeed(ClimbPhysics.MaxRunSpeed), Is.GreaterThan(ClimbPhysics.JumpSpeed(0f)));

    [Test]
    public void JumpSpeed_StandingStill_ReachesTheNextFloor()
    {
        var apex = ClimbPhysics.JumpSpeed(0f) * ClimbPhysics.JumpSpeed(0f) / (2f * ClimbPhysics.Gravity);

        Assert.That(apex, Is.GreaterThan(Layout.FloorSpacing));
    }

    [Test]
    public void Step_JumpFromTheGround_LeavesAndLandsAgain()
    {
        _climber.JumpHeld = true;
        Play(seconds: StepSeconds);
        Assert.That(_climber.IsGrounded, Is.False);
        Assert.That(_velocity.Y, Is.LessThan(0f));

        _climber.JumpHeld = false;
        Play(seconds: 2f);

        Assert.That(_climber.IsGrounded, Is.True);
        Assert.That(_position.Y, Is.EqualTo(Tower.RowOf(_climber.Floor)));
    }

    [Test]
    public void JumpSpeed_AtMaxRunSpeed_ClearsSeveralFloors()
    {
        var speed = ClimbPhysics.JumpSpeed(ClimbPhysics.MaxRunSpeed);
        var apex = speed * speed / (2f * ClimbPhysics.Gravity);

        Assert.That(apex, Is.GreaterThan(4 * Layout.FloorSpacing));
    }

    [Test]
    public void Step_FallingOntoAHighPlatform_LandsThere()
    {
        const int floor = 6;
        var platform = _tower.GetFloor(floor);
        _climber.IsGrounded = false;
        _position = new Vector2(platform.Left, Tower.RowOf(floor) - .5f);
        _velocity = new Vector2(0f, 20f);

        Play(seconds: .5f);

        Assert.That(_highestLanding, Is.EqualTo(floor));
        Assert.That(_climber.IsGrounded, Is.True);
        Assert.That(_position.Y, Is.EqualTo(Tower.RowOf(floor)));
    }

    [Test]
    public void Step_WalkOffAPlatform_Falls()
    {
        var floor = FirstFloorNotTouchingTheLeftWall();
        var platform = _tower.GetFloor(floor);
        _climber.Floor = floor;
        _position = new Vector2(platform.Left, Tower.RowOf(floor));
        _climber.RunDirection = -1f;

        Play(seconds: .5f);

        Assert.That(_climber.Floor, Is.LessThan(floor));
        Assert.That(_position.Y, Is.GreaterThan(Tower.RowOf(floor)));
    }

    [Test]
    public void Step_RisingThroughAPlatform_DoesNotLandOnIt()
    {
        _climber.IsGrounded = false;
        _position.Y = Tower.RowOf(1) + 1f;
        _velocity.Y = -30f;

        var landed = ClimbPhysics.Step(ref _climber, ref _position, ref _velocity, _tower, StepSeconds);

        Assert.That(landed, Is.EqualTo(ClimbPhysics.NoLanding));
        Assert.That(_position.Y, Is.LessThan(Tower.RowOf(1) + 1f));
    }

    private void Play(float seconds)
    {
        for (var time = 0f; time < seconds; time += StepSeconds)
        {
            var landed = ClimbPhysics.Step(ref _climber, ref _position, ref _velocity, _tower, StepSeconds);
            if (landed > _highestLanding)
                _highestLanding = landed;
        }
    }

    private int FirstFloorNotTouchingTheLeftWall()
    {
        var floor = 1;
        while (_tower.GetFloor(floor).Left <= Layout.InnerLeft + ClimbPhysics.BodyWidth)
            floor++;
        return floor;
    }
}
