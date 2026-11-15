using System;
using Doom.Graphics;
using Doom.Maps;

namespace Doom.Play;

/// <summary>Which texture of a switch line changed, so a repeatable switch can flip back.</summary>
public enum SwitchPart
{
    Upper,
    Middle,
    Lower
}

public sealed partial class Level
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Sector specials.</summary>
    private const int SecretSector = 9;

    private const int ScrollingWall = 48;

    /// <summary>Tics a repeatable switch shows its pressed face.</summary>
    private const int SwitchTics = 35;

    /// <summary>Door top stops this far below the lowest neighbouring ceiling.</summary>
    private const float DoorGap = 4;

    /// <summary>Tics a player stands still after teleporting.</summary>
    private const float TeleportZ = 0;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Sets off <paramref name="line"/>'s special if <paramref name="trigger"/> fits it. Players need the right key;
    /// monsters only set off the specials marked for them. Single-use specials clear the line afterwards; switches
    /// change their texture. Returns true if something happened.
    /// </summary>
    public bool ActivateLine(Line line, Actor activator, LineTrigger trigger, bool fromFront)
    {
        var action = LineActions.Find(line.Special);
        if (action == null || action.Trigger != trigger)
            return false;
        if (!activator.IsPlayer && !action.MonstersToo)
            return false;
        if (action.Effect == LineEffect.Teleport && !fromFront)
            return false;
        if (trigger == LineTrigger.Use && !fromFront && !action.OnBackSector)
            return false;

        if (action.Key != KeyCards.None && (Player.Keys & action.Key) == 0)
        {
            if (activator.IsPlayer)
                Say(action.Key switch
                {
                    KeyCards.Blue => DoomTexts.NeedBlueKey,
                    KeyCards.Yellow => DoomTexts.NeedYellowKey,
                    _ => DoomTexts.NeedRedKey
                });
            return false;
        }

        var happened = Perform(action, line, activator);
        if (!happened)
            return false;

        if (trigger != LineTrigger.Walk && !action.OnBackSector)
            FlipSwitch(line, action.Repeatable);
        if (!action.Repeatable)
            line.Special = 0;
        return true;
    }

    //**********************************************************
    //** internals: line actions
    //**********************************************************

    private bool Perform(LineAction action, Line line, Actor activator)
    {
        switch (action.Effect)
        {
            case LineEffect.Exit:
                Exited = true;
                return true;
            case LineEffect.SecretExit:
                Exited = true;
                SecretExit = true;
                return true;
            case LineEffect.Teleport:
                return Teleport(line, activator);
        }

        if (action.OnBackSector)
            return line.BackSector != null && UseDoor(action, line.BackSector);

        var any = false;
        foreach (var sector in Map.Sectors)
        {
            if (sector.Tag != line.Tag || line.Tag == 0)
                continue;

            any |= StartEffect(action, sector);
        }

        return any;
    }

    /// <summary>A door used directly: starts it, or turns a moving or open door around.</summary>
    private bool UseDoor(LineAction action, Sector sector)
    {
        if (sector.CeilingBusy)
        {
            if (action.Effect != LineEffect.DoorOpenWaitClose)
                return false;

            var door = FindMover(sector, true);
            if (door is not { Kind: MoverKind.Door })
                return false;

            if (door.Direction < 0)
            {
                door.Direction = 1;
                door.Target = door.Home;
            }
            else
            {
                door.Direction = -1;
                door.Target = door.Away;
            }

            return true;
        }

        return StartEffect(action, sector);
    }

    private SectorMover FindMover(Sector sector, bool ceiling)
    {
        foreach (var mover in _movers)
        {
            if (mover.Sector == sector && mover.MovesCeiling == ceiling && !mover.Finished)
                return mover;
        }

        return null;
    }

    private bool StartEffect(LineAction action, Sector sector)
    {
        var speed = action.Speed;
        switch (action.Effect)
        {
            case LineEffect.DoorOpenWaitClose:
            case LineEffect.DoorOpenStay:
            {
                var open = LowestNeighbourCeiling(sector) - DoorGap;
                var kind = action.Effect == LineEffect.DoorOpenStay ? MoverKind.OneWay : MoverKind.Door;
                return StartMover(sector, true, kind, speed, open, open, sector.FloorHeight, 1);
            }
            case LineEffect.DoorClose:
                return StartMover(sector, true, MoverKind.OneWay, speed, sector.FloorHeight, sector.CeilingHeight,
                    sector.FloorHeight, -1);
            case LineEffect.DoorCloseWaitOpen:
                return StartMover(sector, true, MoverKind.DoorCloseWaitOpen, speed, sector.FloorHeight,
                    sector.CeilingHeight, sector.FloorHeight, -1);
            case LineEffect.FloorLowerToLowest:
                return MoveFloor(sector, speed, MathF.Min(sector.FloorHeight, LowestNeighbourFloor(sector)));
            case LineEffect.FloorLowerToHighest:
                return MoveFloor(sector, speed, HighestNeighbourFloor(sector, sector.FloorHeight));
            case LineEffect.FloorLowerToHighestPlus8:
            {
                var target = HighestNeighbourFloor(sector, sector.FloorHeight);
                if (target != sector.FloorHeight)
                    target += 8;
                return MoveFloor(sector, speed, target);
            }
            case LineEffect.FloorRaiseToLowestCeiling:
                return MoveFloor(sector, speed, MathF.Min(sector.CeilingHeight, LowestNeighbourCeiling(sector)));
            case LineEffect.FloorRaiseToLowestCeilingMinus8:
                return MoveFloor(sector, speed, MathF.Min(sector.CeilingHeight, LowestNeighbourCeiling(sector)) - 8);
            case LineEffect.FloorRaiseToNextHigher:
                return MoveFloor(sector, speed, NextHigherNeighbourFloor(sector));
            case LineEffect.FloorRaise24:
                return MoveFloor(sector, speed, sector.FloorHeight + 24);
            case LineEffect.Lift:
            {
                var low = MathF.Min(sector.FloorHeight, LowestNeighbourFloor(sector));
                return StartMover(sector, false, MoverKind.Lift, speed, low, sector.FloorHeight, low, -1);
            }
            case LineEffect.Stairs8:
                return BuildStairs(sector, speed, 8);
            case LineEffect.Stairs16:
                return BuildStairs(sector, speed, 16);
            case LineEffect.CeilingLowerToFloor:
                return StartMover(sector, true, MoverKind.OneWay, speed, sector.FloorHeight, sector.CeilingHeight,
                    sector.FloorHeight, -1);
            case LineEffect.CeilingLowerToFloorPlus8:
                return StartMover(sector, true, MoverKind.OneWay, speed, sector.FloorHeight + 8,
                    sector.CeilingHeight, sector.FloorHeight + 8, -1);
            case LineEffect.CeilingRaiseToHighest:
                return StartMover(sector, true, MoverKind.OneWay, speed, HighestNeighbourCeiling(sector),
                    sector.CeilingHeight, sector.CeilingHeight, 1);
            case LineEffect.Crusher:
                return StartMover(sector, true, MoverKind.Crusher, speed, sector.FloorHeight + 8,
                    sector.CeilingHeight, sector.FloorHeight + 8, -1);
            case LineEffect.LightToBrightestNeighbour:
                sector.LightLevel = BrightestNeighbourLight(sector);
                return true;
            case LineEffect.LightTo255:
                sector.LightLevel = 255;
                return true;
            case LineEffect.LightTo35:
                sector.LightLevel = 35;
                return true;
            case LineEffect.LightToDimmestNeighbour:
                sector.LightLevel = DimmestNeighbourLight(sector, sector.LightLevel);
                return true;
            case LineEffect.LightStartStrobe:
                _lights.Add(new LightEffect
                {
                    Sector = sector, Kind = LightKind.SlowStrobe, Max = sector.LightLevel,
                    Min = DimmestNeighbourLight(sector, sector.LightLevel), Timer = 1
                });
                return true;
            default:
                return false;
        }
    }

    private bool MoveFloor(Sector sector, float speed, float target)
    {
        var direction = target >= sector.FloorHeight ? 1 : -1;
        return StartMover(sector, false, MoverKind.OneWay, speed, target, sector.FloorHeight, target, direction);
    }

    private bool StartMover(Sector sector, bool ceiling, MoverKind kind, float speed, float target, float home,
        float away, int direction)
    {
        if (ceiling ? sector.CeilingBusy : sector.FloorBusy)
            return false;

        if (ceiling)
            sector.CeilingBusy = true;
        else
            sector.FloorBusy = true;

        _movers.Add(new SectorMover
        {
            Sector = sector, Kind = kind, MovesCeiling = ceiling, Speed = speed, Target = target, Home = home,
            Away = away, Direction = direction
        });
        return true;
    }

    /// <summary>
    /// Raises the tagged sector by one step, then the sector beyond each of its lines that shares its floor flat,
    /// one step more each, until the chain ends.
    /// </summary>
    private bool BuildStairs(Sector first, float speed, float step)
    {
        if (first.FloorBusy)
            return false;

        var sector = first;
        var height = first.FloorHeight + step;
        var flat = first.FloorFlat;
        var guard = 0;
        while (sector != null && guard++ < Map.Sectors.Length)
        {
            MoveFloor(sector, speed, height);
            Sector next = null;
            foreach (var line in sector.Lines)
            {
                if (line.Back == null || line.FrontSector != sector)
                    continue;

                var candidate = line.BackSector;
                if (candidate.FloorFlat != flat || candidate.FloorBusy)
                    continue;

                next = candidate;
                break;
            }

            sector = next;
            height += step;
        }

        return true;
    }

    private bool Teleport(Line line, Actor actor)
    {
        if (actor.Has(ThingFlags.Missile))
            return false;

        foreach (var destination in _teleportDestinations)
        {
            if (destination.Sector.Tag != line.Tag)
                continue;

            var oldX = actor.X;
            var oldY = actor.Y;
            CheckPosition(actor, destination.X, destination.Y, out var check);
            if (check.BlockingActor != null && !check.BlockingActor.IsPlayer && actor.IsPlayer)
                Kill(check.BlockingActor, actor);

            actor.X = destination.X;
            actor.Y = destination.Y;
            actor.Sector = destination.Sector;
            actor.FloorZ = destination.Sector.FloorHeight;
            actor.CeilingZ = destination.Sector.CeilingHeight;
            actor.Z = actor.Has(ThingFlags.Float) ? actor.Z : actor.FloorZ + TeleportZ;
            actor.Angle = destination.Angle;
            actor.MomX = 0;
            actor.MomY = 0;
            Spawn(ThingTypes.TeleportFog, oldX, oldY, float.NaN);
            Spawn(ThingTypes.TeleportFog, destination.X + MathF.Cos(destination.Angle) * 20,
                destination.Y + MathF.Sin(destination.Angle) * 20, float.NaN);
            if (actor.IsPlayer)
                Player.CurrentViewHeight = Player.ViewHeight;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Swaps a switch line's <c>SW1</c>/<c>SW2</c> texture; a repeatable switch flips back after a second.
    /// </summary>
    private void FlipSwitch(Line line, bool repeatable)
    {
        var side = line.Front;
        if (side == null)
            return;

        if (TryFlip(side, SwitchPart.Upper, repeatable, line) || TryFlip(side, SwitchPart.Middle, repeatable, line))
            return;

        TryFlip(side, SwitchPart.Lower, repeatable, line);
    }

    private bool TryFlip(Side side, SwitchPart part, bool repeatable, Line line)
    {
        var texture = part switch
        {
            SwitchPart.Upper => side.Upper,
            SwitchPart.Middle => side.Middle,
            _ => side.Lower
        };
        var partner = Textures.SwitchPartner(texture);
        if (partner == TextureLibrary.NoTexture)
            return false;

        SetSidePart(side, part, partner);
        if (repeatable)
            _switchTimers.Add((line, texture, SwitchTics, part));
        return true;
    }

    private static void SetSidePart(Side side, SwitchPart part, int texture)
    {
        switch (part)
        {
            case SwitchPart.Upper:
                side.Upper = texture;
                break;
            case SwitchPart.Middle:
                side.Middle = texture;
                break;
            default:
                side.Lower = texture;
                break;
        }
    }

    //**********************************************************
    //** internals: per tic
    //**********************************************************

    private void TickSwitches()
    {
        for (var i = _switchTimers.Count - 1; i >= 0; i--)
        {
            var (line, texture, tics, part) = _switchTimers[i];
            if (--tics > 0)
            {
                _switchTimers[i] = (line, texture, tics, part);
                continue;
            }

            SetSidePart(line.Front, part, texture);
            _switchTimers.RemoveAt(i);
        }
    }

    private void TickScrollingWalls()
    {
        var lines = Map.Lines;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Special == ScrollingWall && lines[i].Front != null)
                lines[i].Front.XOffset += 1;
        }
    }

    private void TickMovers()
    {
        for (var i = 0; i < _movers.Count; i++)
            TickMover(_movers[i]);

        _movers.RemoveAll(static mover => mover.Finished);
    }

    private void TickMover(SectorMover mover)
    {
        var sector = mover.Sector;
        if (mover.Direction == 0)
        {
            if (--mover.WaitTics > 0)
                return;

            switch (mover.Kind)
            {
                case MoverKind.Door:
                    mover.Direction = -1;
                    mover.Target = mover.Away;
                    break;
                default:
                    mover.Direction = 1;
                    mover.Target = mover.Home;
                    break;
            }

            return;
        }

        var height = mover.Height;
        var next = height + mover.Direction * mover.Speed;
        var reached = mover.Direction > 0 ? next >= mover.Target : next <= mover.Target;
        if (reached)
            next = mover.Target;

        if (WouldCrush(mover, next))
        {
            switch (mover.Kind)
            {
                case MoverKind.Door when mover.Direction < 0:
                    mover.Direction = 1;
                    mover.Target = mover.Home;
                    return;
                case MoverKind.Crusher:
                    if (Tic % 4 == 0)
                        CrushThings(mover);
                    next = height + mover.Direction * mover.Speed * 0.125f;
                    reached = false;
                    break;
                default:
                    return;
            }
        }

        mover.Height = next;
        sector.Moved = true;
        if (!reached)
            return;

        switch (mover.Kind)
        {
            case MoverKind.Door when mover.Direction > 0:
                mover.Direction = 0;
                mover.WaitTics = SectorMover.DoorWait;
                return;
            case MoverKind.DoorCloseWaitOpen when mover.Direction < 0:
                mover.Direction = 0;
                mover.WaitTics = SectorMover.LongDoorWait;
                return;
            case MoverKind.Lift when mover.Direction < 0:
                mover.Direction = 0;
                mover.WaitTics = SectorMover.LiftWait;
                return;
            case MoverKind.Crusher:
                mover.Direction = -mover.Direction;
                mover.Target = mover.Direction > 0 ? mover.Home : mover.Away;
                return;
        }

        mover.Finished = true;
        if (mover.MovesCeiling)
            sector.CeilingBusy = false;
        else
            sector.FloorBusy = false;
    }

    /// <summary>Whether moving the plane to <paramref name="height"/> would squeeze a thing in the sector.</summary>
    private bool WouldCrush(SectorMover mover, float height)
    {
        var sector = mover.Sector;
        var ceiling = mover.MovesCeiling ? height : sector.CeilingHeight;
        var floor = mover.MovesCeiling ? sector.FloorHeight : height;
        var shrinking = mover.MovesCeiling ? mover.Direction < 0 : mover.Direction > 0;
        if (!shrinking)
            return false;

        for (var i = 0; i < _actors.Count; i++)
        {
            var actor = _actors[i];
            if (actor.Removed || actor.Sector != sector || !actor.Has(ThingFlags.Solid))
                continue;
            if (ceiling - floor < actor.Height)
                return true;
        }

        return false;
    }

    private void CrushThings(SectorMover mover)
    {
        for (var i = 0; i < _actors.Count; i++)
        {
            var actor = _actors[i];
            if (!actor.Removed && actor.Sector == mover.Sector && actor.Has(ThingFlags.Shootable))
                Damage(actor, null, null, SectorMover.CrushDamage);
        }
    }

    private void StartLightEffect(Sector sector)
    {
        var dimmest = DimmestNeighbourLight(sector, sector.LightLevel);
        var min = dimmest == sector.LightLevel ? 0 : dimmest;
        LightKind kind;
        switch (sector.Special)
        {
            case 1:
                kind = LightKind.RandomFlicker;
                break;
            case 2:
            case 4:
            case 13:
                kind = LightKind.FastStrobe;
                break;
            case 3:
            case 12:
                kind = LightKind.SlowStrobe;
                break;
            case 8:
                kind = LightKind.Glow;
                min = dimmest;
                break;
            case 17:
                kind = LightKind.FireFlicker;
                min = dimmest + 16;
                break;
            default:
                return;
        }

        var synced = sector.Special is 12 or 13;
        _lights.Add(new LightEffect
        {
            Sector = sector, Kind = kind, Max = sector.LightLevel, Min = Math.Min(min, sector.LightLevel),
            Timer = synced ? 1 : Random.Next(1, 8)
        });
    }

    private void TickLights()
    {
        for (var i = 0; i < _lights.Count; i++)
            TickLight(_lights[i]);
    }

    private void TickLight(LightEffect light)
    {
        var sector = light.Sector;
        switch (light.Kind)
        {
            case LightKind.Glow:
                sector.LightLevel += light.Direction * 8;
                if (sector.LightLevel <= light.Min)
                {
                    sector.LightLevel = light.Min;
                    light.Direction = 1;
                }
                else if (sector.LightLevel >= light.Max)
                {
                    sector.LightLevel = light.Max;
                    light.Direction = -1;
                }

                return;
            case LightKind.FireFlicker:
                if (--light.Timer > 0)
                    return;
                sector.LightLevel = Math.Max(light.Min, light.Max - Random.Next(4) * 16);
                light.Timer = 4;
                return;
        }

        if (--light.Timer > 0)
            return;

        var bright = sector.LightLevel == light.Max;
        sector.LightLevel = bright ? light.Min : light.Max;
        light.Timer = light.Kind switch
        {
            LightKind.RandomFlicker => bright ? Random.Next(1, 65) : Random.Next(1, 9),
            LightKind.FastStrobe => bright ? 15 : 5,
            _ => bright ? 35 : 5
        };
    }

    /// <summary>Secrets, damaging floors, and the end-of-episode floor (special 11).</summary>
    private void TickSectorSpecial(Player player)
    {
        var body = player.Body;
        var sector = body.Sector;
        if (sector.Special == SecretSector)
        {
            sector.Special = 0;
            Stats.Secrets++;
            Say(DoomTexts.SecretFound);
            return;
        }

        if (body.Z > sector.FloorHeight || Tic % FloorDamageInterval != 0)
            return;

        var damage = sector.Special switch
        {
            5 => 10,
            7 => 5,
            4 or 11 or 16 => 20,
            _ => 0
        };
        if (damage == 0)
            return;

        if (player.RadiationSuitLeft == 0 || (sector.Special == 16 && Random.Next(256) < 5))
            DamagePlayer(damage);
        if (sector.Special == 11 && player.Health <= 10)
            Exited = true;
    }

    //**********************************************************
    //** internals: neighbour heights and lights
    //**********************************************************

    private static float LowestNeighbourCeiling(Sector sector)
    {
        var lowest = float.MaxValue;
        foreach (var other in Map.Neighbours(sector))
            lowest = MathF.Min(lowest, other.CeilingHeight);

        return lowest == float.MaxValue ? sector.CeilingHeight : lowest;
    }

    private static float HighestNeighbourCeiling(Sector sector)
    {
        var highest = sector.CeilingHeight;
        foreach (var other in Map.Neighbours(sector))
            highest = MathF.Max(highest, other.CeilingHeight);

        return highest;
    }

    private static float LowestNeighbourFloor(Sector sector)
    {
        var lowest = sector.FloorHeight;
        foreach (var other in Map.Neighbours(sector))
            lowest = MathF.Min(lowest, other.FloorHeight);

        return lowest;
    }

    private static float HighestNeighbourFloor(Sector sector, float fallback)
    {
        var highest = float.MinValue;
        foreach (var other in Map.Neighbours(sector))
            highest = MathF.Max(highest, other.FloorHeight);

        return highest == float.MinValue ? fallback : highest;
    }

    private static float NextHigherNeighbourFloor(Sector sector)
    {
        var next = float.MaxValue;
        foreach (var other in Map.Neighbours(sector))
        {
            if (other.FloorHeight > sector.FloorHeight)
                next = MathF.Min(next, other.FloorHeight);
        }

        return next == float.MaxValue ? sector.FloorHeight : next;
    }

    private static int BrightestNeighbourLight(Sector sector)
    {
        var brightest = sector.LightLevel;
        foreach (var other in Map.Neighbours(sector))
            brightest = Math.Max(brightest, other.LightLevel);

        return brightest;
    }

    private static int DimmestNeighbourLight(Sector sector, int fallback)
    {
        var dimmest = fallback;
        foreach (var other in Map.Neighbours(sector))
            dimmest = Math.Min(dimmest, other.LightLevel);

        return dimmest;
    }
}
