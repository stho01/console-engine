using System;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Scenery;
using Doom.Graphics;
using Doom.Maps;
using Doom.Play;
using Doom.Rendering;
using Doom.Rendering.Meshes;

namespace Doom.Scenes;

/// <summary>
/// Plays one map. Input becomes a <see cref="PlayerCommand"/>; the <see cref="Level"/> runs in fixed 1/35 s tics
/// (at most <see cref="MaxTicsPerFrame"/> per frame), and every frame draws the view (or the automap), the weapon,
/// and the HUD. Exiting goes to the <see cref="IntermissionScene"/>; dying and pressing use restarts the map with the
/// loadout it began with; ESC returns to the title.
/// </summary>
public sealed class LevelScene(string mapName, PlayerLoadout loadout) : Scene<DoomGame>
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const double TicSeconds = 1.0 / Level.TicRate;
    private const int MaxTicsPerFrame = 5;
    private const float AutomapZoomStep = 1.25f;

    /// <summary>Light tables for light amplification (nearly full bright) and invulnerability (inverse grey).</summary>
    private const int LightAmpTable = 1;

    private const int InvulnerabilityTable = 32;

    private Level _level;
    private FrameBuffer _frame;
    private WorldRenderer _world;
    private MeshRenderer _mesh; // built the first time the view is switched to it
    private WeaponRenderer _weapon;
    private Automap _automap;
    private readonly Hud _hud = new();
    private int _sky;
    private double _accumulator;
    private bool _showAutomap;

    // Presses are latched until the next tic runs, so a short tap between tics is not lost.
    private bool _usePressed;
    private bool _runToggled;
    private int _slotPressed;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    public override void OnLoad()
    {
        var assets = Game.Wad;
        var map = MapLoader.Load(assets.Wad, mapName, assets.Textures);
        _level = Level.Create(map, assets.Textures, assets.Sprites, loadout, seed: Environment.TickCount);
        _frame = new FrameBuffer(Game.Layout.ViewWidth, Game.Layout.ViewHeight);
        _world = new WorldRenderer(_frame, assets.Palette, assets.Textures);
        _weapon = new WeaponRenderer(_frame, assets.Palette, assets.Sprites);
        _automap = new Automap(assets.Palette) { Zoom = Automap.DefaultZoom / Game.Layout.PixelScale };
        _sky = assets.SkyFor(mapName);
    }

    public override void OnUnload()
    {
        _level.Dispose();
        _mesh?.Dispose();
    }

    public override void OnUpdate()
    {
        var input = Input.Instance;
        if (input.GetKey(Key.ESCAPE).Pressed)
        {
            Game.Scenes.Set(new TitleScene());
            return;
        }

        ReadToggles(input);
        var command = ReadCommand(input);
        _accumulator = Math.Min(_accumulator + GameTime.Delta.TotalSeconds, MaxTicsPerFrame * TicSeconds);
        while (_accumulator >= TicSeconds)
        {
            _accumulator -= TicSeconds;
            command.Use = _usePressed;
            command.ToggleRun = _runToggled;
            command.WeaponSlot = _slotPressed;
            _level.Tick(command);
            _usePressed = false;
            _runToggled = false;
            _slotPressed = 0;

            if (_level.Exited)
            {
                Game.Scenes.Set(new IntermissionScene(mapName, _level.SecretExit, _level.Stats, _level.Tic,
                    _level.Player.ToLoadout()));
                return;
            }

            if (_level.RestartRequested)
            {
                Game.Scenes.Set(new LevelScene(mapName, loadout));
                return;
            }
        }
    }

    public override void OnRender()
    {
        var player = _level.Player;
        if (_showAutomap)
        {
            _automap.Draw(_frame, _level);
        }
        else
        {
            ref var body = ref _level.PlayerBody;
            var fixedLight = FixedLight(player);
            ViewRenderer().Render(_level, new Camera(body.X, body.Y, player.ViewZ, body.Angle), _sky, fixedLight);
            _weapon.Draw(player, _level.Tic, _level.Map.Sectors[body.Sector].LightLevel, fixedLight);
        }

        var assets = Game.Wad;
        Presenter.Present(_frame, assets.Shades, player.TintPalette(), Console);
        _hud.Draw(Console, _level, mapName, Game.Layout.HudTop, Game.ViewMode);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private IViewRenderer ViewRenderer()
    {
        if (Game.ViewMode == ViewMode.Bsp)
            return _world;

        var assets = Game.Wad;
        return _mesh ??= new MeshRenderer(_frame, assets.Palette, assets.Textures, _level.Map);
    }

    private int FixedLight(Player player)
    {
        var colorMaps = Game.Wad.Palette.ColorMaps.Length / Palette.ColorCount;
        if (player.InvulnerabilityLeft > 4 * 32 || (player.InvulnerabilityLeft & 8) != 0)
            return colorMaps > InvulnerabilityTable ? InvulnerabilityTable : 0;
        if (player.LightAmpLeft > 4 * 32 || (player.LightAmpLeft & 8) != 0)
            return LightAmpTable;

        return -1;
    }

    private void ReadToggles(Input input)
    {
        if (input.GetKey(Key.TAB).Pressed)
            _showAutomap = !_showAutomap;
        if (input.GetKey(Key.V).Pressed)
            Game.ViewMode = Game.ViewMode == ViewMode.Bsp ? ViewMode.Mesh : ViewMode.Bsp;
        if (input.GetKey(Key.F11).Pressed)
            Game.Wad.Shades.SetGamma(Game.Wad.Shades.GammaLevel + 1);
        if (input.GetKey(Key.PRIOR).Pressed)
            _automap.Zoom = Math.Max(Automap.MinZoom / Game.Layout.PixelScale, _automap.Zoom / AutomapZoomStep);
        if (input.GetKey(Key.NEXT).Pressed)
            _automap.Zoom = Math.Min(Automap.MaxZoom / Game.Layout.PixelScale, _automap.Zoom * AutomapZoomStep);
        if (input.GetKey(Key.E).Pressed || input.GetKey(Key.ENTER).Pressed)
            _usePressed = true;
        if (input.GetKey(Key.R).Pressed)
            _runToggled = true;

        for (var slot = 1; slot <= 7; slot++)
        {
            if (input.GetKey((int)Key.K0 + slot).Pressed)
                _slotPressed = slot;
        }
    }

    private static PlayerCommand ReadCommand(Input input)
    {
        var command = new PlayerCommand();
        if (input.GetKey(Key.W).Held || input.GetKey(Key.UP).Held)
            command.Forward += 1;
        if (input.GetKey(Key.S).Held || input.GetKey(Key.DOWN).Held)
            command.Forward -= 1;
        if (input.GetKey(Key.D).Held)
            command.Strafe += 1;
        if (input.GetKey(Key.A).Held)
            command.Strafe -= 1;
        if (input.GetKey(Key.LEFT).Held)
            command.Turn += 1;
        if (input.GetKey(Key.RIGHT).Held)
            command.Turn -= 1;
        command.Fire = input.GetKey(Key.SPACE).Held;
        return command;
    }
}
