using System.Collections.Generic;

namespace Doom.Play;

/// <summary>How a line special is set off.</summary>
public enum LineTrigger
{
    /// <summary>Pressing use on it (switches, and doors that act on the sector behind them).</summary>
    Use,

    /// <summary>Walking across it.</summary>
    Walk,

    /// <summary>Shooting it.</summary>
    Shoot
}

/// <summary>What a line special does.</summary>
public enum LineEffect
{
    DoorOpenWaitClose,
    DoorOpenStay,
    DoorClose,
    DoorCloseWaitOpen,
    FloorLowerToLowest,
    FloorLowerToHighest,
    FloorLowerToHighestPlus8,
    FloorRaiseToLowestCeiling,
    FloorRaiseToLowestCeilingMinus8,
    FloorRaiseToNextHigher,
    FloorRaise24,
    Lift,
    Stairs8,
    Stairs16,
    CeilingLowerToFloor,
    CeilingLowerToFloorPlus8,
    CeilingRaiseToHighest,
    Crusher,
    Exit,
    SecretExit,
    Teleport,
    LightToBrightestNeighbour,
    LightTo255,
    LightTo35,
    LightToDimmestNeighbour,
    LightStartStrobe
}

/// <summary>
/// One line special. <see cref="OnBackSector"/>: a door used directly (it moves the sector behind the line, not the
/// tagged ones). <see cref="Repeatable"/> specials keep working; the others clear the line's special after use.
/// </summary>
public sealed record LineAction(
    int Special,
    LineTrigger Trigger,
    bool Repeatable,
    LineEffect Effect,
    float Speed = 0,
    KeyCards Key = KeyCards.None,
    bool OnBackSector = false,
    bool MonstersToo = false);

/// <summary>
/// The line specials this example handles, by the number stored in <c>LINEDEFS</c>. Specials not listed do nothing.
/// Speeds are world units per tic.
/// </summary>
public static class LineActions
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const float DoorSpeed = 2;
    public const float FastDoorSpeed = 8;
    public const float FloorSpeed = 1;
    public const float FastFloorSpeed = 4;
    public const float LiftSpeed = 4;
    public const float FastLiftSpeed = 8;
    public const float CeilingSpeed = 1;
    public const float StairSpeed = 0.25f;

    private static readonly Dictionary<int, LineAction> BySpecial = Build();

    //**********************************************************
    //** public methods
    //**********************************************************

    public static LineAction Find(int special) => BySpecial.GetValueOrDefault(special);

    //**********************************************************
    //** internals
    //**********************************************************

    private static Dictionary<int, LineAction> Build()
    {
        var table = new Dictionary<int, LineAction>();

        void Add(LineAction action) => table[action.Special] = action;

        const LineTrigger use = LineTrigger.Use, walk = LineTrigger.Walk, shoot = LineTrigger.Shoot;

        // Doors used directly
        Add(new LineAction(1, use, true, LineEffect.DoorOpenWaitClose, DoorSpeed, OnBackSector: true,
            MonstersToo: true));
        Add(new LineAction(26, use, true, LineEffect.DoorOpenWaitClose, DoorSpeed, KeyCards.Blue, true));
        Add(new LineAction(27, use, true, LineEffect.DoorOpenWaitClose, DoorSpeed, KeyCards.Yellow, true));
        Add(new LineAction(28, use, true, LineEffect.DoorOpenWaitClose, DoorSpeed, KeyCards.Red, true));
        Add(new LineAction(31, use, false, LineEffect.DoorOpenStay, DoorSpeed, OnBackSector: true));
        Add(new LineAction(32, use, false, LineEffect.DoorOpenStay, DoorSpeed, KeyCards.Blue, true));
        Add(new LineAction(33, use, false, LineEffect.DoorOpenStay, DoorSpeed, KeyCards.Red, true));
        Add(new LineAction(34, use, false, LineEffect.DoorOpenStay, DoorSpeed, KeyCards.Yellow, true));
        Add(new LineAction(117, use, true, LineEffect.DoorOpenWaitClose, FastDoorSpeed, OnBackSector: true));
        Add(new LineAction(118, use, false, LineEffect.DoorOpenStay, FastDoorSpeed, OnBackSector: true));

        // Doors on tagged sectors
        Add(new LineAction(2, walk, false, LineEffect.DoorOpenStay, DoorSpeed));
        Add(new LineAction(3, walk, false, LineEffect.DoorClose, DoorSpeed));
        Add(new LineAction(4, walk, false, LineEffect.DoorOpenWaitClose, DoorSpeed, MonstersToo: true));
        Add(new LineAction(16, walk, false, LineEffect.DoorCloseWaitOpen, DoorSpeed));
        Add(new LineAction(29, use, false, LineEffect.DoorOpenWaitClose, DoorSpeed));
        Add(new LineAction(42, use, true, LineEffect.DoorClose, DoorSpeed));
        Add(new LineAction(46, shoot, true, LineEffect.DoorOpenStay, DoorSpeed));
        Add(new LineAction(50, use, false, LineEffect.DoorClose, DoorSpeed));
        Add(new LineAction(61, use, true, LineEffect.DoorOpenStay, DoorSpeed));
        Add(new LineAction(63, use, true, LineEffect.DoorOpenWaitClose, DoorSpeed));
        Add(new LineAction(75, walk, true, LineEffect.DoorClose, DoorSpeed));
        Add(new LineAction(76, walk, true, LineEffect.DoorCloseWaitOpen, DoorSpeed));
        Add(new LineAction(86, walk, true, LineEffect.DoorOpenStay, DoorSpeed));
        Add(new LineAction(90, walk, true, LineEffect.DoorOpenWaitClose, DoorSpeed));
        Add(new LineAction(103, use, false, LineEffect.DoorOpenStay, DoorSpeed));
        Add(new LineAction(99, use, true, LineEffect.DoorOpenStay, FastDoorSpeed, KeyCards.Blue));
        Add(new LineAction(133, use, false, LineEffect.DoorOpenStay, FastDoorSpeed, KeyCards.Blue));
        Add(new LineAction(134, use, true, LineEffect.DoorOpenStay, FastDoorSpeed, KeyCards.Red));
        Add(new LineAction(135, use, false, LineEffect.DoorOpenStay, FastDoorSpeed, KeyCards.Red));
        Add(new LineAction(136, use, true, LineEffect.DoorOpenStay, FastDoorSpeed, KeyCards.Yellow));
        Add(new LineAction(137, use, false, LineEffect.DoorOpenStay, FastDoorSpeed, KeyCards.Yellow));

        // Floors
        Add(new LineAction(5, walk, false, LineEffect.FloorRaiseToLowestCeiling, FloorSpeed));
        Add(new LineAction(18, use, false, LineEffect.FloorRaiseToNextHigher, FloorSpeed));
        Add(new LineAction(19, walk, false, LineEffect.FloorLowerToHighest, FloorSpeed));
        Add(new LineAction(20, use, false, LineEffect.FloorRaiseToNextHigher, FloorSpeed / 2));
        Add(new LineAction(22, walk, false, LineEffect.FloorRaiseToNextHigher, FloorSpeed / 2));
        Add(new LineAction(23, use, false, LineEffect.FloorLowerToLowest, FloorSpeed));
        Add(new LineAction(24, shoot, false, LineEffect.FloorRaiseToLowestCeiling, FloorSpeed));
        Add(new LineAction(36, walk, false, LineEffect.FloorLowerToHighestPlus8, FastFloorSpeed));
        Add(new LineAction(37, walk, false, LineEffect.FloorLowerToLowest, FloorSpeed));
        Add(new LineAction(38, walk, false, LineEffect.FloorLowerToLowest, FloorSpeed));
        Add(new LineAction(45, use, true, LineEffect.FloorLowerToHighest, FloorSpeed));
        Add(new LineAction(55, use, false, LineEffect.FloorRaiseToLowestCeilingMinus8, FloorSpeed));
        Add(new LineAction(56, walk, false, LineEffect.FloorRaiseToLowestCeilingMinus8, FloorSpeed));
        Add(new LineAction(58, walk, false, LineEffect.FloorRaise24, FloorSpeed));
        Add(new LineAction(59, walk, false, LineEffect.FloorRaise24, FloorSpeed));
        Add(new LineAction(60, use, true, LineEffect.FloorLowerToLowest, FloorSpeed));
        Add(new LineAction(64, use, true, LineEffect.FloorRaiseToLowestCeiling, FloorSpeed));
        Add(new LineAction(65, use, true, LineEffect.FloorRaiseToLowestCeilingMinus8, FloorSpeed));
        Add(new LineAction(69, use, true, LineEffect.FloorRaiseToNextHigher, FloorSpeed));
        Add(new LineAction(70, use, true, LineEffect.FloorLowerToHighestPlus8, FastFloorSpeed));
        Add(new LineAction(71, use, false, LineEffect.FloorLowerToHighestPlus8, FastFloorSpeed));
        Add(new LineAction(82, walk, true, LineEffect.FloorLowerToLowest, FloorSpeed));
        Add(new LineAction(83, walk, true, LineEffect.FloorLowerToHighest, FloorSpeed));
        Add(new LineAction(84, walk, true, LineEffect.FloorLowerToLowest, FloorSpeed));
        Add(new LineAction(91, walk, true, LineEffect.FloorRaiseToLowestCeiling, FloorSpeed));
        Add(new LineAction(92, walk, true, LineEffect.FloorRaise24, FloorSpeed));
        Add(new LineAction(93, walk, true, LineEffect.FloorRaise24, FloorSpeed));
        Add(new LineAction(94, walk, true, LineEffect.FloorRaiseToLowestCeilingMinus8, FloorSpeed));
        Add(new LineAction(95, walk, true, LineEffect.FloorRaiseToNextHigher, FloorSpeed / 2));
        Add(new LineAction(98, walk, true, LineEffect.FloorLowerToHighestPlus8, FastFloorSpeed));
        Add(new LineAction(101, use, false, LineEffect.FloorRaiseToLowestCeiling, FloorSpeed));
        Add(new LineAction(102, use, false, LineEffect.FloorLowerToHighest, FloorSpeed));
        Add(new LineAction(119, walk, false, LineEffect.FloorRaiseToNextHigher, FloorSpeed));
        Add(new LineAction(128, walk, true, LineEffect.FloorRaiseToNextHigher, FloorSpeed));
        Add(new LineAction(129, walk, true, LineEffect.FloorRaiseToNextHigher, FastFloorSpeed));
        Add(new LineAction(130, walk, false, LineEffect.FloorRaiseToNextHigher, FastFloorSpeed));
        Add(new LineAction(131, use, false, LineEffect.FloorRaiseToNextHigher, FastFloorSpeed));
        Add(new LineAction(132, use, true, LineEffect.FloorRaiseToNextHigher, FastFloorSpeed));

        // Lifts
        Add(new LineAction(10, walk, false, LineEffect.Lift, LiftSpeed, MonstersToo: true));
        Add(new LineAction(21, use, false, LineEffect.Lift, LiftSpeed));
        Add(new LineAction(62, use, true, LineEffect.Lift, LiftSpeed));
        Add(new LineAction(88, walk, true, LineEffect.Lift, LiftSpeed, MonstersToo: true));
        Add(new LineAction(120, walk, true, LineEffect.Lift, FastLiftSpeed));
        Add(new LineAction(121, walk, false, LineEffect.Lift, FastLiftSpeed));
        Add(new LineAction(122, use, false, LineEffect.Lift, FastLiftSpeed));
        Add(new LineAction(123, use, true, LineEffect.Lift, FastLiftSpeed));

        // Stairs
        Add(new LineAction(7, use, false, LineEffect.Stairs8, StairSpeed));
        Add(new LineAction(8, walk, false, LineEffect.Stairs8, StairSpeed));
        Add(new LineAction(100, walk, false, LineEffect.Stairs16, FastFloorSpeed));
        Add(new LineAction(127, use, false, LineEffect.Stairs16, FastFloorSpeed));

        // Ceilings and crushers
        Add(new LineAction(40, walk, false, LineEffect.CeilingRaiseToHighest, CeilingSpeed));
        Add(new LineAction(41, use, false, LineEffect.CeilingLowerToFloor, CeilingSpeed));
        Add(new LineAction(43, use, true, LineEffect.CeilingLowerToFloor, CeilingSpeed));
        Add(new LineAction(44, walk, false, LineEffect.CeilingLowerToFloorPlus8, CeilingSpeed));
        Add(new LineAction(72, walk, true, LineEffect.CeilingLowerToFloorPlus8, CeilingSpeed));
        Add(new LineAction(6, walk, false, LineEffect.Crusher, CeilingSpeed * 2));
        Add(new LineAction(25, walk, false, LineEffect.Crusher, CeilingSpeed));
        Add(new LineAction(73, walk, true, LineEffect.Crusher, CeilingSpeed));
        Add(new LineAction(77, walk, true, LineEffect.Crusher, CeilingSpeed * 2));

        // Exits and teleports
        Add(new LineAction(11, use, false, LineEffect.Exit));
        Add(new LineAction(52, walk, false, LineEffect.Exit));
        Add(new LineAction(51, use, false, LineEffect.SecretExit));
        Add(new LineAction(124, walk, false, LineEffect.SecretExit));
        Add(new LineAction(39, walk, false, LineEffect.Teleport, MonstersToo: true));
        Add(new LineAction(97, walk, true, LineEffect.Teleport, MonstersToo: true));

        // Lights
        Add(new LineAction(12, walk, false, LineEffect.LightToBrightestNeighbour));
        Add(new LineAction(13, walk, false, LineEffect.LightTo255));
        Add(new LineAction(17, walk, false, LineEffect.LightStartStrobe));
        Add(new LineAction(35, walk, false, LineEffect.LightTo35));
        Add(new LineAction(79, walk, true, LineEffect.LightTo35));
        Add(new LineAction(80, walk, true, LineEffect.LightToBrightestNeighbour));
        Add(new LineAction(81, walk, true, LineEffect.LightTo255));
        Add(new LineAction(104, walk, false, LineEffect.LightToDimmestNeighbour));
        Add(new LineAction(138, use, true, LineEffect.LightTo255));
        Add(new LineAction(139, use, true, LineEffect.LightTo35));

        return table;
    }
}
