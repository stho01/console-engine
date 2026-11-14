using System;
using System.Collections.Generic;
using System.Numerics;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Physics;
using ConsoleEngine.Rendering3D;
using Kartster.Bots;
using Kartster.Collision;
using Kartster.Components;
using Kartster.Driving;
using Kartster.Effects;
using Kartster.Modes;
using Kartster.PowerUps;
using Kartster.Race;
using Kartster.Scenes.Menus;
using Kartster.Systems;
using Kartster.Ui;
using PlanarVector2 = System.Numerics.Vector2;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace Kartster.Scenes;

/// <summary>
/// A race on the track: the player's kart and <see cref="BotCount"/> bots on the track's starting grid
/// (<see cref="RaceMarkers.Grid"/>; the player on pole, as Kartster lines the keyboard player up before the bots),
/// driven by Kartster's kart model (<see cref="KartPhysicsSystem"/>) on the engine's <see cref="PhysicsSystem"/> at
/// Kartster's 120 steps per second, with a chase camera behind the player. A <see cref="RaceSession"/> run by the
/// <see cref="RaceSystem"/> counts laps; nobody moves during its countdown. Bots drive by the
/// <see cref="BotDriverSystem"/> in the game's kart folders' karts, each in its own colour. Karts push each other
/// through their box colliders and stop at the track's walls and posts (<see cref="TrackCollision"/>). Kartster's
/// power-ups (<see cref="PowerUpSystem.Add"/>): pads, bots using them, E/T/B for the player. The HUD
/// (<see cref="Hud"/>, filled by a <see cref="RaceReport"/>) is drawn over the 3D picture, with the
/// <see cref="Minimap"/> in its top-left corner. Keys: R restarts the race from the grid, Enter or Esc pauses
/// (<see cref="PauseMenu"/>: RESUME, RESTART, QUIT TO TITLE). Once the player has finished, the
/// <see cref="AutopilotSystem"/> drives their kart on; <see cref="RaceReport.ResultsDelaySeconds"/> later a
/// <see cref="RaceFinish"/> runs the race to its end for the karts still out, then the <see cref="ResultsScene"/>
/// follows.
/// </summary>
/// <param name="lapCount">Laps to race, at least 1.</param>
/// <param name="botCount">Bots on the grid besides the player.</param>
/// <param name="timeTrial">A time trial (with no bots): records the laps and shows the best one as a ghost.</param>
public sealed class TrackScene(int lapCount, int botCount = TrackScene.DefaultBotCount, TimeTrial timeTrial = null)
    : EcsScene<KartsterGame>, IUiLayer
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Kartster's fixed step (<c>PhysicsTuning</c>).</summary>
    public const float StepSeconds = 1f / 120f;

    /// <summary>Kartster's most steps per frame.</summary>
    public const int MaxStepsPerFrame = 8;

    public const int DefaultBotCount = 5;

    /// <summary>The player's grid slot: the front, as Kartster lines up the keyboard player before the bots.</summary>
    public const int PlayerSlot = 0;

    /// <summary>
    /// Stand-in starting grid for a track without <c>race.txt</c>: metres between rows, and either side of the
    /// centerline. Rows are also this far apart where the exported grid runs out (Kartster's <c>ExtendGrid</c>).
    /// </summary>
    public const float GridRowSpacing = 6f;
    public const float GridSideOffset = 2f;

    /// <summary>How far either side of its point the stand-in start line reaches, metres.</summary>
    public const float StandInLineReach = 20f;

    /// <summary>
    /// <see cref="Bot.Skill"/> of the front bot, and how much less the last one has. Kept under 1, the bots' best
    /// pace on Star Circuit, so a good driver can beat them.
    /// </summary>
    public const float TopBotSkill = 0.8f;
    public const float BotSkillSpread = 0.5f;

    /// <summary>Metres right of the centerline each bot drives, by bot number (cycled).</summary>
    private static readonly float[] LaneOffsets = [-1.2f, 1.2f, -0.4f, 0.4f, -2f, 2f];

    /// <summary>
    /// Bot body colours, by bot number (cycled): entries of <c>KartsterGame.ScenePalette</c> (brown, white,
    /// near-black, navy, pale blue), so their lit shades stay near colours palette mode has.
    /// </summary>
    private static readonly uint[] BotRgb = [0xB06A30, 0xF0F0F0, 0x2B2B2D, 0x0F2A78, 0xA2BEF0];

    /// <summary>Loads kart parts in this colour, never drawn: <see cref="KartModel.Recolor"/> replaces it.</summary>
    private static readonly Vector3 BodyKey = new(-1f);

    private const float MetresPerSecondToKmh = 3.6f;

    private readonly Hud _hud = new();
    private Entity _player;
    private KartPhysicsSystem _physics;
    private RaceReport _report;
    private HudState _hudState;
    private readonly PauseMenu _pause = new();
    private KartEffectsSystem _effects;
    private RaceFinish _finish;
    private bool _finishing;
    private Minimap _minimap;
    private Query _karts;
    private PlanarVector2[] _kartPositions;
    private uint[] _kartColors;
    private PowerUpSystem _powerUps;

    //**********************************************************
    //** props
    //**********************************************************

    public int BotCount => botCount;
    public int LapCount => lapCount;

    /// <summary>The race; karts are registered by <see cref="Kart.Index"/>.</summary>
    public RaceSession Race { get; private set; }

    /// <summary>The player's kart model.</summary>
    public KartDynamics Dynamics => _physics.GetDynamics(World.Get<Kart>(_player).Index);

    /// <summary>The player's speed, metres per second.</summary>
    public float Speed => World.Get<Velocity>(_player).Value.Length();

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Where grid slot <paramref name="slot"/> is on a track without <c>race.txt</c>: two karts a row,
    /// <see cref="GridSideOffset"/> left (even slots) and right (odd) of the centerline, rows
    /// <see cref="GridRowSpacing"/> apart, the first half a row behind route distance 0. Returns the kart position
    /// (world axes) and the way the route runs there.
    /// </summary>
    public static (Vector3 Position, Vector3 Forward) StandInGridSlot(Track track, int slot)
    {
        var row = slot / 2;
        var (center, forward) = track.Sample(-GridRowSpacing * (row + 0.5f));
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        return (center + right * (slot % 2 == 0 ? -GridSideOffset : GridSideOffset), forward);
    }

    /// <summary>
    /// Where the kart in grid slot <paramref name="slot"/> starts: <paramref name="markers"/>' grid slot; past its
    /// end, as Kartster's <c>ExtendGrid</c>, <see cref="GridRowSpacing"/> behind the kart two slots ahead; with no
    /// markers (or fewer than two slots), <see cref="StandInGridSlot"/>.
    /// </summary>
    public static GridSlot StartingSlot(Track track, RaceMarkers markers, int slot)
    {
        var grid = markers?.Grid ?? [];
        if (slot < grid.Length)
            return grid[slot];

        if (grid.Length >= 2)
        {
            var ahead = StartingSlot(track, markers, slot - 2);
            var (sin, cos) = MathF.SinCos(ahead.Heading);
            var forward = new PlanarVector2(-sin, -cos); // heading 0 faces -Z, positive turns left
            return ahead with { Position = ahead.Position - forward * GridRowSpacing };
        }

        var (position, direction) = StandInGridSlot(track, slot);
        return new GridSlot(new PlanarVector2(position.X, position.Z), KartFrame.HeadingOf(direction));
    }

    /// <summary>
    /// The race on <paramref name="track"/> over <paramref name="laps"/> laps: <paramref name="markers"/>' start line
    /// and checkpoints, or without markers a start line across the centerline at route distance 0 and no checkpoints.
    /// </summary>
    public static RaceSession CreateSession(Track track, RaceMarkers markers, int laps)
    {
        if (markers != null)
            return new RaceSession(markers.Start, markers.Checkpoints, laps);

        var (point, forward) = track.Sample(0f);
        var line = new RaceGate(new PlanarVector2(point.X, point.Z),
            PlanarVector2.Normalize(new PlanarVector2(forward.X, forward.Z)), StandInLineReach, StandInLineReach);
        return new RaceSession(line, [], laps);
    }

    /// <summary>
    /// Creates one kart per tuning on the grid (<see cref="StartingSlot"/>) at the road's height, slot =
    /// <see cref="Kart.Index"/> = the tuning's index. The kart at <paramref name="playerIndex"/> gets
    /// <see cref="PlayerKart"/>, the others a <see cref="Bot"/>: the nearer the front, the more skilled
    /// (<see cref="TopBotSkill"/> at the front, <see cref="BotSkillSpread"/> less at the back), each on its own lane.
    /// With a <paramref name="race"/>, every kart gets a <see cref="RaceProgress"/> and is registered by its index in
    /// grid order (the player as human), so the race starts ranked as the grid.
    /// </summary>
    public static Entity[] CreateKarts(World world, Track track, RaceMarkers markers, KartTuning[] tunings,
        int playerIndex, RaceSession race = null)
    {
        var karts = new Entity[tunings.Length];
        var bots = playerIndex >= 0 && playerIndex < tunings.Length ? tunings.Length - 1 : tunings.Length;
        var botNumber = 0;
        for (var i = 0; i < tunings.Length; i++)
        {
            var slot = StartingSlot(track, markers, i);
            var kart = KartEntity.Create(world, tunings[i], new XnaVector2(slot.Position.X, slot.Position.Y),
                slot.Heading, i);
            track.TryGetRoadHeight(new Vector3(slot.Position.X, 0f, slot.Position.Y), out world.Get<Kart>(kart).Height);
            if (i == playerIndex)
            {
                world.Add(kart, new PlayerKart());
            }
            else
            {
                world.Add(kart, new Bot
                {
                    Skill = TopBotSkill - BotSkillSpread * botNumber / Math.Max(1, bots - 1),
                    LaneOffset = LaneOffsets[botNumber % LaneOffsets.Length],
                    Sample = -1,
                });
                botNumber++;
            }

            if (race != null)
            {
                world.Add(kart, new RaceProgress());
                race.Register(i, isHuman: i == playerIndex);
            }

            karts[i] = kart;
        }

        return karts;
    }

    /// <summary>
    /// The fixed-step systems that drive the karts, in run order: <see cref="BotDriverSystem"/> →
    /// <see cref="KartPhysicsSystem"/> → <see cref="PhysicsSystem"/> → <see cref="KartGroundSystem"/>, then, with a
    /// <paramref name="race"/>, its <see cref="RaceSystem"/> (and the bots hold still during its countdown).
    /// </summary>
    public static KartPhysicsSystem AddDriving(SystemGroup fixedStep, Track track, KartTuning[] tunings,
        RaceSession race = null)
    {
        var physics = new KartPhysicsSystem(tunings);
        var bots = fixedStep.Add(new BotDriverSystem(track, physics, race));
        fixedStep.Add(physics);
        // One substep, so a step integrates the kart model's forces exactly once, as Kartster's single 1/120 s step
        // does (the force is held through all substeps, and the tyres cancel a whole step's sideways speed). Kart
        // contacts are soft (30 Hz at one substep) and over-damped, which is enough for karts pushing each other.
        bots.Contacts = fixedStep.Add(new PhysicsSystem { SubstepCount = 1, EnableSleep = false });
        fixedStep.Add(new KartGroundSystem(track));
        if (race != null)
            fixedStep.Add(new RaceSystem(race));
        return physics;
    }

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        var track = Game.Track;
        var count = botCount + 1;
        var tunings = new KartTuning[count];
        var meshes = new Mesh[count];
        var names = new string[count];
        _kartColors = new uint[count];
        _kartPositions = new PlanarVector2[count];
        LoadBotKarts(tunings, meshes, names, PlayerSlot);
        tunings[PlayerSlot] = Game.Tuning;
        meshes[PlayerSlot] = Game.Kart;
        names[PlayerSlot] = Game.KartName.ToUpperInvariant();

        Race = CreateSession(track, Game.Markers, lapCount);
        var karts = CreateKarts(World, track, Game.Markers, tunings, PlayerSlot, Race);
        _player = karts[PlayerSlot];
        TrackCollision.Load(System.IO.Path.Combine("Assets", "Tracks"), track.Name).AddTo(World);
        _report = new RaceReport(Race, names, PlayerSlot);

        FixedTimestep = new FixedTimestep(StepSeconds, MaxStepsPerFrame);
        _physics = AddDriving(FixedSimulation, track, tunings, Race);
        _powerUps = PowerUpSystem.Add(FixedSimulation, Simulation, track, _physics, Race);
        FixedSimulation.Add(new AutopilotSystem());
        _finish = new RaceFinish(Race, FixedSimulation, StepSeconds);

        Simulation.Add(new KartInputSystem(race: Race));

        _karts = World.Query(Query.With<Kart, Position>());
        _minimap = CreateMinimap(track, Game.Markers);

        var skidMarks = new SkidMarks(Track.Asphalt, count);
        var smoke = new SlideSmoke(kartCount: count);
        // In Presentation, as the simulation stops while paused and the pause menu's keys must still be read.
        Presentation.Add(new FlowSystem(this));
        Presentation.Add(new InterpolationSystem(FixedTimestep));
        _effects = Presentation.Add(new KartEffectsSystem(_physics, skidMarks, smoke));
        var render = Presentation.Add(new KartRenderSystem(_player, Game, meshes, skidMarks, smoke)
            { PowerUps = new PowerUpView(_powerUps.Field) });
        timeTrial?.AddTo(FixedSimulation, Presentation, Race, render, Game, FixedTimestep);
    }

    /// <summary>
    /// The HUD and minimap after the 3D frame is finished, then the pause menu over them; while the race is being
    /// finished, only <see cref="RaceFinish.Draw"/>.
    /// </summary>
    public void DrawUi(IPixelTarget target)
    {
        if (_finishing && !_pause.IsOpen)
        {
            _finish.Draw(target);
            return;
        }

        _report.FillHud(ref _hudState, Speed * MetresPerSecondToKmh);
        _hudState.PowerUp = _powerUps.HudName(PlayerSlot);
        timeTrial?.DrawUi(target, ref _hudState);
        _hud.Draw(target, _hudState);

        foreach (var chunk in _karts.GetChunks())
        {
            var karts = chunk.GetSpan<Kart>();
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
                _kartPositions[karts[i].Index] = new PlanarVector2(positions[i].Value.X, positions[i].Value.Y);
        }

        _minimap.Draw(target, _kartPositions, _kartColors, PlayerSlot, World.Get<Rotation>(_player).Radians);
        _pause.Draw(target);
    }

    /// <summary>
    /// The minimap of <paramref name="track"/>, its start tick on <paramref name="markers"/>' start line, or without
    /// markers across route distance 0.
    /// </summary>
    public static Minimap CreateMinimap(Track track, RaceMarkers markers)
    {
        if (markers != null)
            return Minimap.FromTrack(track, markers.Start.Point, markers.Start.Forward);

        var (point, forward) = track.Sample(0f);
        return Minimap.FromTrack(track, new PlanarVector2(point.X, point.Z),
            PlanarVector2.Normalize(new PlanarVector2(forward.X, forward.Z)));
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>
    /// Bots take the game's kart folders in turn, each kart's parts loaded once and recoloured per bot, named after
    /// the kart and their number ("WAGON 2").
    /// </summary>
    private void LoadBotKarts(KartTuning[] tunings, Mesh[] meshes, string[] names, int playerIndex)
    {
        var folders = Game.KartFolders;
        var loaded = new Dictionary<string, Mesh>();
        var botNumber = 0;
        for (var i = 0; i < tunings.Length; i++)
        {
            if (i == playerIndex)
                continue;

            var kartFolder = folders[botNumber % folders.Length];
            if (!loaded.TryGetValue(kartFolder, out var mesh))
                loaded[kartFolder] = mesh = KartModel.Load(kartFolder, BodyKey);
            tunings[i] = KartCatalog.ForKart(System.IO.Path.GetFileName(kartFolder));
            var rgb = BotRgb[botNumber % BotRgb.Length];
            meshes[i] = KartModel.Recolor(mesh, BodyKey, Rgb(rgb));
            _kartColors[i] = rgb;
            names[i] = $"{System.IO.Path.GetFileName(kartFolder).ToUpperInvariant()} {botNumber + 1}";
            botNumber++;
        }
    }

    /// <summary>
    /// Enter or Esc: pause; while paused, the <see cref="PauseMenu"/>'s keys. R: the same race again. Once the race is
    /// over for the player, it is finished (<see cref="RaceFinish"/>) and the results follow.
    /// </summary>
    private void UpdateFlow(float seconds)
    {
        var input = Input.Instance;
        if (_pause.IsOpen)
        {
            switch (_pause.Update(MenuKeys.From(input)))
            {
                case PauseChoice.Resume:
                    SetPaused(false);
                    break;
                case PauseChoice.Restart:
                    Game.Scenes.Set(new TrackScene(lapCount, botCount, timeTrial));
                    break;
                case PauseChoice.QuitToTitle:
                    Game.Scenes.Set(new TitleScene());
                    break;
            }

            return;
        }

        if (input.GetKey(Key.ESCAPE).Pressed || input.GetKey(Key.ENTER).Pressed)
        {
            _pause.Open();
            SetPaused(true);
        }
        else if (input.GetKey(Key.R).Pressed)
        {
            Game.Scenes.Set(new TrackScene(lapCount, botCount, timeTrial));
        }
        else if (_report.Update(seconds))
        {
            _finishing = true;
            if (_finish.Update())
                Game.Scenes.Set(new ResultsScene(_report.Results(Game.Track.Name.ToUpperInvariant())));
        }
    }

    /// <summary>
    /// Stops or restarts the simulation (<see cref="EcsScene{TGame}.IsSimulationPaused"/>) and the smoke; drawing
    /// goes on.
    /// </summary>
    private void SetPaused(bool paused)
    {
        IsSimulationPaused = paused;
        _effects.Enabled = !paused;
    }

    /// <summary>Runs <see cref="UpdateFlow"/> once a frame, before the drawing, paused or not.</summary>
    private sealed class FlowSystem(TrackScene scene) : EntitySystem
    {
        protected override void OnUpdate(float deltaSeconds) => scene.UpdateFlow(deltaSeconds);
    }

    private static Vector3 Rgb(uint rgb) =>
        new Vector3((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF) / 255f;
}
