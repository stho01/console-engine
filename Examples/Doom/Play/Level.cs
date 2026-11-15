using System;
using System.Collections.Generic;
using ConsoleEngine.Ecs;
using Doom.Components;
using Doom.Graphics;
using Doom.Maps;
using Doom.Systems;

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
/// A running level: the map, the player, and everything that moves (doors, lifts, lights). The things in it
/// (monsters, pickups, decorations, projectiles, effects and the player's body) are entities in <see cref="World"/>,
/// run by the systems in <c>Systems/</c>; the level keeps the map, its collision and the rules the systems share
/// (damage, line specials). The simulation runs in fixed tics of 1/35 s (<see cref="Tick"/>), like Doom; speeds and
/// timers are per tic. Split over partial files: movement and collision, combat, monsters, the player, and line/sector
/// specials. Dispose it to free the world.
/// </summary>
public sealed partial class Level : IDisposable
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int TicRate = 35;
    public const float Gravity = 1f;

    /// <summary>The highest step a walker climbs.</summary>
    public const float MaxStepHeight = 24;

    private const float TicSeconds = 1f / TicRate;

    /// <summary>Tics a message stays on screen.</summary>
    private const int MessageTics = 4 * TicRate;

    /// <summary>Thing flag bits: which skill levels a map thing appears on, deaf, and multiplayer only.</summary>
    private const int SkillEasy = 1, SkillMedium = 2, SkillHard = 4, AmbushFlag = 8, MultiplayerOnly = 16;

    private readonly SystemGroup _systems;

    /// <summary>Every thing, the player's body too: what blocks, gets shot and gets crushed.</summary>
    private readonly Query _bodies;

    private readonly List<SpawnRequest> _spawned = [];
    private readonly List<SectorMover> _movers = [];
    private readonly List<LightEffect> _lights = [];
    private readonly List<Entity> _teleportDestinations = [];
    private readonly List<Line> _walkLines = [];
    private readonly List<(Line Line, int Texture, int Tics, SwitchPart Part)> _switchTimers = [];
    private readonly bool[] _heardSound;
    private readonly int[] _soundBlocks;
    private readonly Queue<(Sector Sector, int Blocks)> _soundQueue = new();
    private bool _ticking;

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
        _bodies = World.Query(Query.With<Thing, Body>());
        Things = World.Query(Query.With<Thing, Body, Animation>());
        _systems = new SystemGroup(World);
        _systems.Add(new PickupSystem(this));
        _systems.Add(new MonsterSystem(this));
        _systems.Add(new MissileSystem(this));
        _systems.Add(new DyingSystem(this));
        _systems.Add(new StaticThingSystem(this));
    }

    //**********************************************************
    //** props
    //**********************************************************

    public Map Map { get; }
    public TextureLibrary Textures { get; }
    public SpriteLibrary Sprites { get; }
    public Random Random { get; }

    /// <summary>The level's things as entities. Disposed with the level.</summary>
    public World World { get; } = new();

    /// <summary>Every thing but the player's body: the ones with an <see cref="Animation"/>, which can be drawn.</summary>
    public Query Things { get; }

    public Player Player { get; private set; }

    /// <summary>The player's body: position, angle and footprint.</summary>
    public ref Body PlayerBody => ref World.Get<Body>(Player.Body);

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

            var entity = level.CreateThing(new SpawnRequest(info, thing.X, thing.Y, float.NaN)
            {
                Angle = thing.Angle,
                Ambush = (thing.Flags & AmbushFlag) != 0
            });
            if (thing.Type == ThingTypes.TeleportDestination)
                level._teleportDestinations.Add(entity);
            if (info.Has(ThingFlags.CountKill))
                level.Stats.TotalKills++;
            if (info.Has(ThingFlags.CountItem))
                level.Stats.TotalItems++;
        }

        var playerInfo = ThingTypes.Player;
        var body = level.Place(playerInfo, start.X, start.Y, float.NaN);
        body.Angle = start.Angle;
        var player = level.World.Create(
            new Thing { Type = playerInfo.Id, Flags = playerInfo.Flags, Health = playerInfo.Health },
            body,
            new Momentum());
        level.Player = new Player(player, loadout, body.Z);

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

        return level;
    }

    /// <summary>
    /// Runs one tic: movers and lights, the player, then the thing systems (pickups, monsters, missiles, dying things,
    /// static things). Things removed during the tic are destroyed and things spawned join at its end.
    /// </summary>
    public void Tick(in PlayerCommand command)
    {
        _ticking = true;
        Tic++;
        foreach (var sector in Map.Sectors)
            sector.Moved = false;

        TickMovers();
        TickLights();
        TickSwitches();
        TickScrollingWalls();
        TickPlayer(command);
        _systems.Update(TicSeconds);
        _ticking = false;

        FlushSpawned();
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
    /// <paramref name="z"/> is NaN. Between tics it exists at once; during a tic it joins at the end of the tic and
    /// this returns <see cref="Entity.Null"/>.
    /// </summary>
    public Entity Spawn(ThingInfo info, float x, float y, float z)
    {
        var request = new SpawnRequest(info, x, y, z);
        if (!_ticking)
            return CreateThing(request);

        _spawned.Add(request);
        return Entity.Null;
    }

    public bool IsPlayer(Entity entity) => entity == Player.Body;

    public void Dispose() => World.Dispose();

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Everything a new thing starts with. The extras default to a still thing facing east.</summary>
    private readonly record struct SpawnRequest(ThingInfo Info, float X, float Y, float Z)
    {
        public float Angle { get; init; }
        public Momentum Momentum { get; init; }
        public Entity Owner { get; init; }
        public bool Dropped { get; init; }
        public bool Ambush { get; init; }
    }

    /// <summary>Queues a thing to join the level at the end of the tic (or of the next one, between tics).</summary>
    private void Enqueue(in SpawnRequest request) => _spawned.Add(request);

    /// <summary>Takes a thing out of play now; its entity is destroyed when the systems' commands play back.</summary>
    internal void Remove(Entity entity)
    {
        World.Get<Thing>(entity).Removed = true;
        _systems.Commands.Destroy(entity);
    }

    /// <summary>
    /// Creates the entity: every thing has a <see cref="Thing"/>, <see cref="Body"/> and <see cref="Animation"/>;
    /// monsters and missiles also move (<see cref="Momentum"/>) and get <see cref="Monster"/> or
    /// <see cref="Missile"/>, pickups are <see cref="Collectible"/>, and things that explode get
    /// <see cref="Explosive"/>.
    /// </summary>
    private Entity CreateThing(in SpawnRequest request)
    {
        var info = request.Info;
        var thing = new Thing
        {
            Type = info.Id, Flags = info.Flags, Health = info.Health, Dropped = request.Dropped,
            Ambush = request.Ambush
        };
        var body = Place(info, request.X, request.Y, request.Z);
        body.Angle = request.Angle;
        var animation = new Animation { Sprite = Sprites.IdOf(info.Sprite) };
        Animator.Play(ref animation, info, AnimationClip.Idle, info.FrameTics, true);

        if (info.Monster != null)
        {
            thing.State = ThingState.Idle;
            return World.Create(thing, body, animation, request.Momentum, new Monster { ReactionTics = 8 });
        }

        if (info.Has(ThingFlags.Missile))
        {
            thing.State = ThingState.Flying;
            return World.Create(thing, body, animation, request.Momentum, new Missile { Owner = request.Owner });
        }

        if (info.IsEffect)
        {
            thing.State = ThingState.Dying;
            Animator.Play(ref animation, info, AnimationClip.Death, info.FrameTics, false);
            return World.Create(thing, body, animation);
        }

        if (info.Has(ThingFlags.Pickup))
            return World.Create(thing, body, animation, new Collectible());
        if (info.SplashDamage > 0)
            return World.Create(thing, body, animation, new Explosive());

        return World.Create(thing, body, animation);
    }

    private Body Place(ThingInfo info, float x, float y, float z)
    {
        var sector = Map.SectorAt(x, y);
        var body = new Body
        {
            X = x, Y = y, Radius = info.Radius, Height = info.Height, Sector = sector.Index,
            FloorZ = sector.FloorHeight, CeilingZ = sector.CeilingHeight
        };
        if (!float.IsNaN(z))
            body.Z = z;
        else if (info.Has(ThingFlags.Hanging))
            body.Z = sector.CeilingHeight - info.Height;
        else if (info.Has(ThingFlags.Float) && info.Has(ThingFlags.Monster))
            body.Z = Math.Min(sector.FloorHeight + 32, sector.CeilingHeight - info.Height);
        else
            body.Z = sector.FloorHeight;
        return body;
    }

    private void FlushSpawned()
    {
        for (var i = 0; i < _spawned.Count; i++)
            CreateThing(_spawned[i]);
        _spawned.Clear();
    }

    /// <summary>Keeps a thing on its floor (or ceiling, hanging) after the sector moved.</summary>
    internal void SettleOnFloor(Entity entity)
    {
        ref var thing = ref World.Get<Thing>(entity);
        ref var body = ref World.Get<Body>(entity);
        if ((thing.Flags & ThingFlags.Hanging) != 0)
        {
            body.Z = Map.Sectors[body.Sector].CeilingHeight - body.Height;
            return;
        }

        if ((thing.Flags & ThingFlags.Float) != 0 && thing.State is not ThingState.Dying and not ThingState.Dead)
            return;

        CheckPosition(entity, body.X, body.Y, out var check);
        body.FloorZ = check.FloorZ;
        body.CeilingZ = check.CeilingZ;
        body.Z = check.FloorZ;
    }

    /// <summary>Whether the sector under <paramref name="body"/> moved this tic.</summary>
    internal bool SectorMoved(in Body body) => Map.Sectors[body.Sector].Moved;

    internal int RandomRange(int min, int max) => Random.Next(min, max + 1);
}
