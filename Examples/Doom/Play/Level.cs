using System;
using System.Collections.Generic;
using Doom.Graphics;
using Doom.Maps;

namespace Doom.Play;

/// <summary>One tic of player input: movement and turning in -1..1, buttons, and a weapon slot (0 = none).</summary>
public struct PlayerCommand
{
    public float Forward;
    public float Strafe;
    public float Turn;
    public bool Fire;
    public bool Use;
    public int WeaponSlot;
    public bool ToggleRun;
}

/// <summary>Kills, items and secrets: found so far and in the level.</summary>
public sealed class LevelStats
{
    public int Kills { get; set; }
    public int TotalKills { get; set; }
    public int Items { get; set; }
    public int TotalItems { get; set; }
    public int Secrets { get; set; }
    public int TotalSecrets { get; set; }
}

/// <summary>
/// A running level: the map, its things, the player, and everything that moves (doors, lifts, lights). The
/// simulation runs in fixed tics of 1/35 s (<see cref="Tick"/>), like Doom; speeds and timers are per tic.
/// Split over partial files: movement and collision, combat, monsters, the player, and line/sector specials.
/// </summary>
public sealed partial class Level
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int TicRate = 35;
    public const float Gravity = 1f;

    /// <summary>The highest step a walker climbs.</summary>
    public const float MaxStepHeight = 24;

    /// <summary>Tics a message stays on screen.</summary>
    private const int MessageTics = 4 * TicRate;

    /// <summary>Thing flag bits: which skill levels a map thing appears on, deaf, and multiplayer only.</summary>
    private const int SkillEasy = 1, SkillMedium = 2, SkillHard = 4, AmbushFlag = 8, MultiplayerOnly = 16;

    private readonly List<Actor> _actors = [];
    private readonly List<Actor> _spawned = [];
    private readonly List<SectorMover> _movers = [];
    private readonly List<LightEffect> _lights = [];
    private readonly List<Actor> _teleportDestinations = [];
    private readonly List<Line> _walkLines = [];
    private readonly List<(Line Line, int Texture, int Tics, SwitchPart Part)> _switchTimers = [];
    private readonly bool[] _heardSound;
    private readonly int[] _soundBlocks;
    private readonly Queue<(Sector Sector, int Blocks)> _soundQueue = new();

    //**********************************************************
    //** ctor
    //**********************************************************

    private Level(Map map, TextureLibrary textures, SpriteLibrary sprites, int seed)
    {
        Map = map;
        Textures = textures;
        Sprites = sprites;
        Random = new Random(seed);
        _heardSound = new bool[map.Sectors.Length];
        _soundBlocks = new int[map.Sectors.Length];
    }

    //**********************************************************
    //** props
    //**********************************************************

    public Map Map { get; }
    public TextureLibrary Textures { get; }
    public SpriteLibrary Sprites { get; }
    public Random Random { get; }
    public Player Player { get; private set; }
    public IReadOnlyList<Actor> Actors => _actors;
    public IReadOnlyList<SectorMover> Movers => _movers;
    public LevelStats Stats { get; } = new();

    /// <summary>Tics since the level started.</summary>
    public int Tic { get; private set; }

    public string Message { get; private set; }
    public int MessageLeft { get; private set; }

    /// <summary>Set when an exit is used; <see cref="SecretExit"/> tells which.</summary>
    public bool Exited { get; private set; }

    public bool SecretExit { get; private set; }

    /// <summary>The dead player pressed use or fire: the scene restarts the level.</summary>
    public bool RestartRequested { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Spawns the map's things for a skill level (0-1 easy, 2 medium, 3-4 hard), puts the player on start 1 with
    /// <paramref name="loadout"/>, and starts the sector light effects.
    /// </summary>
    public static Level Create(Map map, TextureLibrary textures, SpriteLibrary sprites, PlayerLoadout loadout,
        int skill = 2, int seed = 1)
    {
        var level = new Level(map, textures, sprites, seed);
        var skillBit = skill <= 1 ? SkillEasy : skill == 2 ? SkillMedium : SkillHard;
        var start = new MapThing(0, 0, 0, ThingTypes.PlayerStart, 0);
        var hasStart = false;
        foreach (var thing in map.Things)
        {
            if (thing.Type == ThingTypes.PlayerStart && !hasStart)
            {
                start = thing;
                hasStart = true;
                continue;
            }

            if ((thing.Flags & MultiplayerOnly) != 0 || (thing.Flags & skillBit) == 0)
                continue;

            var info = ThingTypes.Find(thing.Type);
            if (info == null)
                continue;

            var actor = level.Spawn(info, thing.X, thing.Y, float.NaN);
            actor.Angle = thing.Angle;
            actor.Ambush = (thing.Flags & AmbushFlag) != 0;
            if (thing.Type == ThingTypes.TeleportDestination)
                level._teleportDestinations.Add(actor);
            if (info.Has(ThingFlags.CountKill))
                level.Stats.TotalKills++;
            if (info.Has(ThingFlags.CountItem))
                level.Stats.TotalItems++;
        }

        var body = new Actor(ThingTypes.Player, sprites.Find(ThingTypes.Player.Sprite)) { IsPlayer = true };
        level.Place(body, start.X, start.Y, float.NaN);
        body.Angle = start.Angle;
        level._actors.Add(body);
        level.Player = new Player(body, loadout);

        foreach (var sector in map.Sectors)
        {
            if (sector.Special == SecretSector)
                level.Stats.TotalSecrets++;
            level.StartLightEffect(sector);
        }

        foreach (var line in map.Lines)
        {
            if (line.Special != 0 && LineActions.Find(line.Special) is { Trigger: LineTrigger.Walk })
                level._walkLines.Add(line);
        }

        level.FlushSpawned();
        return level;
    }

    /// <summary>Runs one tic: the player, every thing, movers, lights, then removes what died.</summary>
    public void Tick(in PlayerCommand command)
    {
        Tic++;
        foreach (var sector in Map.Sectors)
            sector.Moved = false;

        TickMovers();
        TickLights();
        TickSwitches();
        TickScrollingWalls();
        TickPlayer(command);

        for (var i = 0; i < _actors.Count; i++)
        {
            var actor = _actors[i];
            if (!actor.Removed && !actor.IsPlayer)
                TickActor(actor);
        }

        FlushSpawned();
        _actors.RemoveAll(static actor => actor.Removed);
        Textures.Animate(Tic);
        if (MessageLeft > 0)
            MessageLeft--;
    }

    /// <summary>Shows a message in the HUD for a few seconds.</summary>
    public void Say(string message)
    {
        Message = message;
        MessageLeft = MessageTics;
    }

    /// <summary>
    /// Creates a thing at (x, y). Its height is the floor (or ceiling, for hanging things) of the sector there when
    /// <paramref name="z"/> is NaN. Things spawned during a tic join the level at the end of it.
    /// </summary>
    public Actor Spawn(ThingInfo info, float x, float y, float z)
    {
        var actor = new Actor(info, info.Sprite == null ? null : Sprites.Find(info.Sprite));
        Place(actor, x, y, z);
        if (info.Monster != null)
        {
            actor.State = ActorState.Idle;
            actor.ReactionTics = 8;
        }

        if (info.Has(ThingFlags.Missile))
            actor.State = ActorState.Flying;
        if (info.IsEffect)
        {
            actor.State = ActorState.Dying;
            actor.Play(info.DeathFrames, info.FrameTics, false);
        }

        _spawned.Add(actor);
        return actor;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Place(Actor actor, float x, float y, float z)
    {
        actor.X = x;
        actor.Y = y;
        actor.Sector = Map.SectorAt(x, y);
        actor.FloorZ = actor.Sector.FloorHeight;
        actor.CeilingZ = actor.Sector.CeilingHeight;
        if (!float.IsNaN(z))
            actor.Z = z;
        else if (actor.Has(ThingFlags.Hanging))
            actor.Z = actor.Sector.CeilingHeight - actor.Height;
        else if (actor.Has(ThingFlags.Float) && actor.Has(ThingFlags.Monster))
            actor.Z = Math.Min(actor.Sector.FloorHeight + 32, actor.Sector.CeilingHeight - actor.Height);
        else
            actor.Z = actor.Sector.FloorHeight;
    }

    private void FlushSpawned()
    {
        if (_spawned.Count == 0)
            return;

        _actors.AddRange(_spawned);
        _spawned.Clear();
    }

    private void TickActor(Actor actor)
    {
        switch (actor.State)
        {
            case ActorState.Static:
                actor.Animate();
                if (actor.Sector.Moved)
                    SettleOnFloor(actor);
                break;
            case ActorState.Flying:
                TickMissile(actor);
                break;
            case ActorState.Dying:
                TickDying(actor);
                break;
            case ActorState.Dead:
                if (actor.Sector.Moved)
                    SettleOnFloor(actor);
                break;
            default:
                TickMonster(actor);
                break;
        }
    }

    private void TickDying(Actor actor)
    {
        actor.Animate();
        if (actor.PendingSplash > 0 && actor.FrameIndex >= 2)
        {
            var damage = actor.PendingSplash;
            actor.PendingSplash = 0;
            RadiusDamage(actor, actor.Target, damage);
        }

        if (!actor.Has(ThingFlags.Missile) && actor.Sector.Moved)
            SettleOnFloor(actor);
        if (!actor.AnimationDone)
            return;

        if (actor.Info.RemoveAfterDeath)
            actor.Removed = true;
        else
            actor.State = ActorState.Dead;
    }

    /// <summary>Keeps a thing on its floor (or ceiling, hanging) after the sector moved.</summary>
    private void SettleOnFloor(Actor actor)
    {
        if (actor.Has(ThingFlags.Hanging))
        {
            actor.Z = actor.Sector.CeilingHeight - actor.Height;
            return;
        }

        if (actor.Has(ThingFlags.Float) && actor.State is not ActorState.Dying and not ActorState.Dead)
            return;

        CheckPosition(actor, actor.X, actor.Y, out var check);
        actor.FloorZ = check.FloorZ;
        actor.CeilingZ = check.CeilingZ;
        actor.Z = check.FloorZ;
    }

    private int RandomRange(int min, int max) => Random.Next(min, max + 1);
}
