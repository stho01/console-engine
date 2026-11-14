using System;
using System.Numerics;

namespace Kartster.PowerUps;

/// <summary>
/// What each power-up is and how often a pad hands it out: Kartster's numbers (<c>PowerUpCatalog</c>,
/// <c>PowerUpSetCatalog</c>, <c>PowerUpSystem</c>, <c>HazardSimulation</c>, <c>ProjectileSimulation</c>,
/// <c>ThrowSimulation</c>).
/// </summary>
public static class PowerUpCatalog
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Seconds a taken pad stays dormant (<c>PowerUpSystem.RespawnSeconds</c>).</summary>
    public const float RespawnSeconds = 5f;

    /// <summary>Radius round a pad a kart's box must touch to take it, metres (<c>PickUpRadiusMeters</c>).</summary>
    public const float PickUpRadius = 1.2f;

    /// <summary>Seconds a drawn power-up spins on the reel before it can be used (<c>SpinSeconds</c>).</summary>
    public const float SpinSeconds = 2.5f;

    /// <summary>Faces on a reel, and how long one shows at the start and the end of a spin.</summary>
    public const int ReelLength = 48;
    public const float FastFaceSeconds = 0.04f;
    public const float SlowFaceSeconds = 0.34f;

    /// <summary>Speed boost: +9 m/s at once, then 1.6× power and 1.2× grip for 10 s.</summary>
    public const float BoostImpulse = 9f;
    public const float BoostSeconds = 10f;
    public const float BoostPower = 1.6f;
    public const float BoostGrip = 1.2f;

    /// <summary>Engine tune 1.35× power, grip tune 1.3× grip, both 10 s.</summary>
    public const float TuneSeconds = 10f;
    public const float EngineTunePower = 1.35f;
    public const float GripTuneGrip = 1.3f;

    /// <summary>
    /// A banana: 1.3 m, armed after 0.4 s, until somebody hits it; spins the kart at 6 rad/s, keeps its speed, and
    /// leaves a tenth of its grip, climbing back over 2.5 s.
    /// </summary>
    public const int BananaCharges = 5;
    public const float BananaRadius = 1.3f;
    public const float BananaArmingSeconds = 0.4f;
    public const float BananaSpin = 6f;
    public const float BananaGrip = 0.1f;
    public const float BananaGripSeconds = 2.5f;

    /// <summary>
    /// A can of lapskaus fired ahead: 50 m/s for 4 s, 2 m in front, hits within 1.6 m once armed (0.1 s). The kart
    /// hit keeps 40% of its speed, spins at 9 rad/s, skids aside at 8 m/s and has a fifth of its grip, back in 1.5 s.
    /// </summary>
    public const float CanSpeed = 50f;
    public const float CanSeconds = 4f;
    public const float CanLaunchOffset = 2f;
    public const float CanRadius = 1.6f;
    public const float CanArmingSeconds = 0.1f;
    public const float CanHitSpeedKept = 0.4f;
    public const float CanSpin = 9f;
    public const float CanSkid = 8f;
    public const float CanGrip = 0.2f;
    public const float CanGripSeconds = 1.5f;

    /// <summary>Across the can's path nearer than this the shooter is straight behind: the kart skids its own way.
    /// </summary>
    public const float SkidSideMeters = 0.5f;

    /// <summary>Joika: a can that chases the kart one place ahead, 44 m/s for 8 s, turning at most 4 rad/s.</summary>
    public const float JoikaSpeed = 44f;
    public const float JoikaSeconds = 8f;
    public const float JoikaTurn = 4f;

    /// <summary>Farther than this a Joika follows the road, aiming this far ahead of itself; nearer, at the kart.
    /// </summary>
    public const float DirectChaseMeters = 25f;
    public const float RoadLookAheadMeters = 12f;

    /// <summary>
    /// A can dropped behind lies on the road (1.3 m, armed after 1 s) until somebody drives into it: stopped dead,
    /// spun at 9 rad/s, the can's grip loss (<c>CanStopImpact</c>, <c>DroppedCan</c>).
    /// </summary>
    public const float DroppedCanRadius = 1.3f;
    public const float DroppedCanArmingSeconds = 1f;

    /// <summary>What is dropped lands this far behind the kart (<c>HazardSimulation.DropDistanceMeters</c>).</summary>
    public const float DropDistance = 2.5f;

    /// <summary>
    /// A banana thrown ahead leaves 1.5 m in front and 1.4 m up with the kart's velocity plus 25 m/s ahead and
    /// 8.5 m/s up, and becomes a banana where it lands (<c>ThrowSimulation</c>).
    /// </summary>
    public const float ThrowAhead = 1.5f;
    public const float ThrowHeight = 1.4f;
    public const float ThrowSpeed = 25f;
    public const float ThrowUpSpeed = 8.5f;
    public const float Gravity = 9.81f;

    /// <summary>Invulnerable 5 s: hazards, cans, blasts, the magnet and the train pass it by; bumps don't move it.
    /// </summary>
    public const float InvulnerableSeconds = 5f;

    /// <summary>
    /// How heavy an invulnerable kart is in a kart contact: Kartster hands the solver an immovable body; the port's
    /// solver needs a mass, so a thousand times its own (<see cref="PowerUpContactSystem"/>).
    /// </summary>
    public const float ImmovableMass = 1000f;

    /// <summary>Magnet: everyone within 26 m (not the invulnerable) is pulled at 14 m/s² towards the kart, 5 s.</summary>
    public const float MagnetPull = 14f;
    public const float MagnetSeconds = 5f;
    public const float MagnetRadius = 26f;

    /// <summary>Anvil: four times as heavy in kart contacts for 8 s.</summary>
    public const float AnvilMass = 4f;
    public const float AnvilSeconds = 8f;

    /// <summary>Lightning: every other kart held to two thirds of its top speed, and drawn half size, for 6 s.</summary>
    public const float LightningTopSpeed = 0.667f;
    public const float LightningShrink = 0.5f;
    public const float LightningSeconds = 6f;

    /// <summary>Vampire: the kart one place ahead at 0.75× power, the user at 1.25×, 8 s. From the lead: nothing.
    /// </summary>
    public const float VampireDrain = 0.75f;
    public const float VampireGain = 1.25f;
    public const float VampireSeconds = 8f;

    /// <summary>
    /// Earthquake and Glitch on every other kart for 5 s (Kartster's disruption time): steering ± up to 0.3 of
    /// full lock at random each step, or left and right swapped.
    /// </summary>
    public const float DisruptionSeconds = 5f;
    public const float EarthquakeSteeringNoise = 0.3f;
    public const float GlitchSteeringScale = -1f;

    /// <summary>
    /// Smoke bomb: dropped like a banana, goes off when its 0.8 s fuse runs out and blasts every kart within 8 m
    /// (the dropper too): down to 60% of its speed, spun up to 5 rad/s, shoved up to 10 m/s from the middle, grip
    /// 0.3 back over 1.2 s, all fading to nothing at the edge. Then a 9 m cloud for 10 s.
    /// </summary>
    public const float SmokeFuseSeconds = 0.8f;
    public const float SmokeSeconds = 10.8f;
    public const float SmokeRadius = 9f;
    public const float SmokeBlastRadius = 8f;
    public const float SmokeBlastSpeedKept = 0.6f;
    public const float SmokeBlastSpin = 5f;
    public const float SmokeBlastShove = 10f;
    public const float SmokeBlastGrip = 0.3f;
    public const float SmokeBlastGripSeconds = 1.2f;
    public const float SmokeDarkness = 0.85f;

    /// <summary>Fake power-up: a pad that is a can lying on the road (1.2 m, armed after 1 s).</summary>
    public const float FakePadRadius = PickUpRadius;
    public const float FakePadArmingSeconds = 1f;

    /// <summary>
    /// Roadblocks, put down 3.5 m behind and turned across the way the kart faces: plastic 2.4 × 0.5 m, 45 kg,
    /// 30 s, sliding to a stop at 6 m/s²; concrete 2 × 0.6 m, fixed, for the race. A kart closing on one faster than
    /// 4 m/s crashes, once a second: plastic keeps 75% of its speed, concrete none.
    /// </summary>
    public const float ObstacleDropDistance = 3.5f;
    public static readonly Vector2 RoadblockHalf = new(1.2f, 0.25f);
    public static readonly Vector2 ConcreteHalf = new(1f, 0.3f);
    public const float RoadblockHeight = 1f;
    public const float ConcreteHeight = 0.8f;
    public const float RoadblockMass = 45f;
    public const float RoadblockSeconds = 30f;
    public const float RoadblockRestitution = 0.3f;
    public const float ConcreteRestitution = 0.05f;
    public const float RoadblockSlideDeceleration = 6f;
    public const float RoadblockSpinDamping = 3f;
    public const float CrashSpeed = 4f;
    public const float CrashCooldownSeconds = 1f;
    public const float RoadblockCrashSpeedKept = 0.75f;

    /// <summary>
    /// The locomotive: engine 3 × 13 m and cart 3 × 8 m (0.8 m apart), 70 m/s along the route, starting 4 m clear of
    /// the kart in its lane and easing onto the centerline over 60 m. Each kart it touches once: 30% of its speed
    /// kept, spun at 6 rad/s, shoved 22 m/s along and 12 m/s off its side. Fades 40 m past the leader (sent back:
    /// the last kart), after 40 s at the latest, over 1.2 s, hitting nobody.
    /// </summary>
    public const float TrainSpeed = 70f;
    public const float TrainHalfWidth = 1.5f;
    public const float EngineLength = 13f;
    public const float CartLength = 8f;
    public const float CouplingGap = 0.8f;
    public const float TrainLength = EngineLength + CouplingGap + CartLength;
    public const float TrainStartClearance = 4f;
    public const float TrainJoinLineMeters = 60f;
    public const float TrainSpeedKept = 0.3f;
    public const float TrainSpin = 6f;
    public const float TrainShoveAlong = 22f;
    public const float TrainShoveAside = 12f;
    public const float TrainPassMargin = 40f;
    public const float TrainFadeSeconds = 1.2f;
    public const float TrainSeconds = 40f;

    private static readonly SetEntry[] NormalSet =
    [
        // Kartster's "normal" set, (front, back) weights, in its order. Three are left out, Trampoline (1.5, 0.5: a
        // launch needs height), Bug Splat (0.3, 1.5) and Smudge (0, 1.5) (they only change a screen): Kartster's roll
        // skips refused entries and the rest keep their ratios (PowerUpSetDefinition.Roll).
        new(PowerUpKind.SpeedBoost, 2f, 6f),
        new(PowerUpKind.EngineTune, 2f, 3f),
        new(PowerUpKind.GripTune, 2f, 3f),
        new(PowerUpKind.Banana, 4f, 1f),
        new(PowerUpKind.Roadblock, 2.5f, 0.5f),
        new(PowerUpKind.ConcreteRoadblock, 1f, 0.2f),
        new(PowerUpKind.FakePowerUp, 1.5f, 0.5f),
        new(PowerUpKind.FakeFinish, 1.5f, 0.5f),
        new(PowerUpKind.SmokeBomb, 2f, 0.5f),
        new(PowerUpKind.Invulnerable, 1.5f, 0.5f),
        new(PowerUpKind.Anvil, 1.5f, 0.3f),
        new(PowerUpKind.Lapskaus, 0f, 2.5f),
        new(PowerUpKind.TripleLapskaus, 0f, 1f),
        new(PowerUpKind.HomingJoika, 0f, 2f),
        new(PowerUpKind.TripleHomingJoika, 0f, 0.8f),
        new(PowerUpKind.Lightning, 0f, 0.8f),
        new(PowerUpKind.Magnet, 0f, 2f),
        new(PowerUpKind.Vampire, 0f, 2f),
        new(PowerUpKind.Earthquake, 0f, 1.5f),
        new(PowerUpKind.Glitch, 0f, 1f),
        new(PowerUpKind.Locomotive, 0f, 2.5f),
    ];

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Kartster's name, upper case for the HUD.</summary>
    public static string Name(PowerUpKind kind) => kind switch
    {
        PowerUpKind.SpeedBoost => "SPEED BOOST",
        PowerUpKind.EngineTune => "ENGINE TUNE",
        PowerUpKind.GripTune => "GRIP TUNE",
        PowerUpKind.Banana => "BANANAS",
        PowerUpKind.Lapskaus => "LAPSKAUS",
        PowerUpKind.TripleLapskaus => "TRIPLE LAPSKAUS",
        PowerUpKind.HomingJoika => "HOMING JOIKA",
        PowerUpKind.TripleHomingJoika => "TRIPLE HOMING JOIKA",
        PowerUpKind.Invulnerable => "INVULNERABLE",
        PowerUpKind.Magnet => "MAGNET",
        PowerUpKind.Anvil => "ANVIL",
        PowerUpKind.Lightning => "LIGHTNING",
        PowerUpKind.Vampire => "VAMPIRE",
        PowerUpKind.Earthquake => "EARTHQUAKE",
        PowerUpKind.Glitch => "GLITCH",
        PowerUpKind.SmokeBomb => "SMOKE BOMB",
        PowerUpKind.FakeFinish => "FAKE FINISH",
        PowerUpKind.FakePowerUp => "FAKE POWER-UP",
        PowerUpKind.Roadblock => "ROADBLOCK",
        PowerUpKind.ConcreteRoadblock => "CONCRETE ROADBLOCK",
        PowerUpKind.Locomotive => "LOCOMOTIVE",
        _ => null,
    };

    /// <summary>Presses one pickup is good for; the slot empties after the last.</summary>
    public static int Charges(PowerUpKind kind) => kind switch
    {
        PowerUpKind.Banana => BananaCharges,
        PowerUpKind.TripleLapskaus or PowerUpKind.TripleHomingJoika => 3,
        PowerUpKind.None => 0,
        _ => 1,
    };

    public static bool IsCan(PowerUpKind kind) => kind is PowerUpKind.Lapskaus or PowerUpKind.TripleLapskaus
        or PowerUpKind.HomingJoika or PowerUpKind.TripleHomingJoika;

    public static bool IsHoming(PowerUpKind kind) => kind is PowerUpKind.HomingJoika or PowerUpKind.TripleHomingJoika;

    /// <summary>Put down behind on its own key, thrown ahead on T (Kartster's <c>CanBeThrown</c>).</summary>
    public static bool IsDropped(PowerUpKind kind) => kind is PowerUpKind.Banana or PowerUpKind.SmokeBomb
        or PowerUpKind.FakePowerUp or PowerUpKind.Roadblock or PowerUpKind.ConcreteRoadblock;

    public static bool IsObstacle(PowerUpKind kind) => kind is PowerUpKind.Roadblock or PowerUpKind.ConcreteRoadblock;

    public static Vector2 ObstacleHalf(PowerUpKind kind) =>
        kind == PowerUpKind.ConcreteRoadblock ? ConcreteHalf : RoadblockHalf;

    /// <summary>
    /// The weight of <paramref name="kind"/> for a kart at <paramref name="normalizedPosition"/> (0 leader, 1 last):
    /// a straight line from its front weight to its back weight (<c>PowerUpSetEntry.WeightAt</c>).
    /// </summary>
    public static float WeightAt(PowerUpKind kind, float normalizedPosition)
    {
        foreach (var entry in NormalSet)
        {
            if (entry.Kind == kind)
                return entry.WeightAt(normalizedPosition);
        }

        return 0f;
    }

    /// <summary>
    /// One weighted draw from the normal set at <paramref name="normalizedPosition"/> (<c>PowerUpSetDefinition.Roll</c>):
    /// nothing weighs anything there, the first entry.
    /// </summary>
    public static PowerUpKind Roll(float normalizedPosition, Random random)
    {
        var total = 0f;
        foreach (var entry in NormalSet)
            total += entry.WeightAt(normalizedPosition);
        if (total <= 0f)
            return NormalSet[0].Kind;

        var target = (float)random.NextDouble() * total;
        foreach (var entry in NormalSet)
        {
            target -= entry.WeightAt(normalizedPosition);
            if (target <= 0f)
                return entry.Kind;
        }

        return NormalSet[^1].Kind; // floating-point drift on the last entry
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private readonly record struct SetEntry(PowerUpKind Kind, float Front, float Back)
    {
        public float WeightAt(float position) =>
            float.Lerp(MathF.Max(Front, 0f), MathF.Max(Back, 0f), Math.Clamp(position, 0f, 1f));
    }
}
