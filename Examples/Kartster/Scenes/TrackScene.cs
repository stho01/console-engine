using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using ConsoleEngine.Rendering3D;
using Kartster.Bots;
using Kartster.Components;
using Kartster.Driving;
using Kartster.Effects;
using Kartster.Systems;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace Kartster.Scenes;

/// <summary>
/// A race on the track: the player's kart and <see cref="BotCount"/> bots on a starting grid behind route distance 0,
/// driven by Kartster's kart model (<see cref="KartPhysicsSystem"/>) on the engine's <see cref="PhysicsSystem"/> at
/// Kartster's 120 steps per second, with a chase camera behind the player. Bots drive by the
/// <see cref="BotDriverSystem"/> and race in the other kart folders' karts, each in its own colour. Karts push each
/// other through their box colliders. The game restarts the scene for R and Tab.
/// </summary>
/// <param name="botCount">Bots on the grid besides the player.</param>
public sealed class TrackScene(int botCount = TrackScene.DefaultBotCount) : EcsScene<KartsterGame>
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Kartster's fixed step (<c>PhysicsTuning</c>).</summary>
    public const float StepSeconds = 1f / 120f;

    /// <summary>Kartster's most steps per frame.</summary>
    public const int MaxStepsPerFrame = 8;

    public const int DefaultBotCount = 5;

    /// <summary>Starting grid: metres between rows, and either side of the centerline.</summary>
    public const float GridRowSpacing = 6f;
    public const float GridSideOffset = 2f;

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
    private static readonly Vector3[] BotColors =
        [Rgb(0xB06A30), Rgb(0xF0F0F0), Rgb(0x2B2B2D), Rgb(0x0F2A78), Rgb(0xA2BEF0)];

    /// <summary>Loads kart parts in this colour, never drawn: <see cref="KartModel.Recolor"/> replaces it.</summary>
    private static readonly Vector3 BodyKey = new(-1f);

    private Entity _player;
    private KartPhysicsSystem _physics;

    //**********************************************************
    //** props
    //**********************************************************

    public int BotCount => botCount;

    /// <summary>The player's kart model.</summary>
    public KartDynamics Dynamics => _physics.GetDynamics(World.Get<Kart>(_player).Index);

    /// <summary>The player's speed, metres per second.</summary>
    public float Speed => World.Get<Velocity>(_player).Value.Length();

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Where grid slot <paramref name="slot"/> is: two karts a row, <see cref="GridSideOffset"/> left (even slots) and
    /// right (odd) of the centerline, rows <see cref="GridRowSpacing"/> apart, the first half a row behind route
    /// distance 0. Returns the kart position (world axes) and the way the route runs there.
    /// </summary>
    /// <remarks>shortcut: a made-up grid; swap for the race's exported grid (<c>race.txt</c>) once it exists.</remarks>
    public static (Vector3 Position, Vector3 Forward) GridSlot(Track track, int slot)
    {
        var row = slot / 2;
        var (center, forward) = track.Sample(-GridRowSpacing * (row + 0.5f));
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        return (center + right * (slot % 2 == 0 ? -GridSideOffset : GridSideOffset), forward);
    }

    /// <summary>
    /// Creates one kart per tuning on the grid, slot = <see cref="Kart.Index"/> = the tuning's index. The kart at
    /// <paramref name="playerIndex"/> gets <see cref="PlayerKart"/>, the others a <see cref="Bot"/>: the nearer the
    /// front, the more skilled (<see cref="TopBotSkill"/> at the front, <see cref="BotSkillSpread"/> less at the
    /// back), each on its own lane.
    /// </summary>
    public static Entity[] CreateKarts(World world, Track track, KartTuning[] tunings, int playerIndex)
    {
        var karts = new Entity[tunings.Length];
        var bots = playerIndex >= 0 && playerIndex < tunings.Length ? tunings.Length - 1 : tunings.Length;
        var botNumber = 0;
        for (var i = 0; i < tunings.Length; i++)
        {
            var (position, forward) = GridSlot(track, i);
            var kart = KartEntity.Create(world, tunings[i], new XnaVector2(position.X, position.Z),
                KartFrame.HeadingOf(forward), i);
            world.Get<Kart>(kart).Height = position.Y;
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

            karts[i] = kart;
        }

        return karts;
    }

    /// <summary>
    /// The fixed-step systems that drive the karts, in run order: <see cref="BotDriverSystem"/> →
    /// <see cref="KartPhysicsSystem"/> → <see cref="PhysicsSystem"/> → <see cref="KartGroundSystem"/>.
    /// </summary>
    public static KartPhysicsSystem AddDriving(SystemGroup fixedStep, Track track, KartTuning[] tunings)
    {
        var physics = new KartPhysicsSystem(tunings);
        fixedStep.Add(new BotDriverSystem(track, physics));
        fixedStep.Add(physics);
        // One substep, so a step integrates the kart model's forces exactly once, as Kartster's single 1/120 s step
        // does (the force is held through all substeps, and the tyres cancel a whole step's sideways speed). Kart
        // contacts are soft (30 Hz at one substep) and over-damped, which is enough for karts pushing each other.
        fixedStep.Add(new PhysicsSystem { SubstepCount = 1, EnableSleep = false });
        fixedStep.Add(new KartGroundSystem(track));
        return physics;
    }

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        var track = Game.Track;
        var count = botCount + 1;
        var playerIndex = count - 1; // the back of the grid
        var tunings = new KartTuning[count];
        var meshes = new Mesh[count];
        LoadBotKarts(tunings, meshes, playerIndex);
        tunings[playerIndex] = Game.Tuning;
        meshes[playerIndex] = Game.Kart;

        var karts = CreateKarts(World, track, tunings, playerIndex);
        _player = karts[playerIndex];

        FixedTimestep = new FixedTimestep(StepSeconds, MaxStepsPerFrame);
        _physics = AddDriving(FixedSimulation, track, tunings);

        Simulation.Add(new KartInputSystem());

        var skidMarks = new SkidMarks(Track.Asphalt, count);
        var smoke = new SlideSmoke(kartCount: count);
        Presentation.Add(new InterpolationSystem(FixedTimestep));
        Presentation.Add(new KartEffectsSystem(_physics, skidMarks, smoke));
        Presentation.Add(new KartRenderSystem(_player, Game, meshes, skidMarks, smoke));
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>
    /// Bots take the kart folders in turn, each kart's parts loaded once and recoloured per bot. Without kart folders
    /// they drive the player's kart, in its colour.
    /// </summary>
    private void LoadBotKarts(KartTuning[] tunings, Mesh[] meshes, int playerIndex)
    {
        // shortcut: the game's kart folder (its first argument) isn't visible here, so this reads the default one;
        // take KartsterGame's folder list once it exposes it.
        var folder = Path.Combine("Assets", "Karts");
        var folders = Directory.Exists(folder) ? Directory.GetDirectories(folder) : [];
        Array.Sort(folders, StringComparer.Ordinal);
        var loaded = new Dictionary<string, Mesh>();
        var botNumber = 0;
        for (var i = 0; i < tunings.Length; i++)
        {
            if (i == playerIndex)
                continue;

            if (folders.Length == 0)
            {
                tunings[i] = Game.Tuning;
                meshes[i] = Game.Kart;
                continue;
            }

            var kartFolder = folders[botNumber % folders.Length];
            if (!loaded.TryGetValue(kartFolder, out var mesh))
                loaded[kartFolder] = mesh = KartModel.Load(kartFolder, BodyKey);
            tunings[i] = KartCatalog.ForKart(Path.GetFileName(kartFolder));
            meshes[i] = KartModel.Recolor(mesh, BodyKey, BotColors[botNumber % BotColors.Length]);
            botNumber++;
        }
    }

    private static Vector3 Rgb(int rgb) =>
        new Vector3((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF) / 255f;
}
