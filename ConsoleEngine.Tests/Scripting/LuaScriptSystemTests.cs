using System;
using System.IO;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Ecs.Systems;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Scripting;
using ConsoleEngine.Tests.Fakes;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Scripting;

public class LuaScriptSystemTests
{
    private const int Width = 20;
    private const int Height = 10;
    private const float Frame = 0.5f;

    private FakeConsoleHandler _handler;
    private RenderConsole _console;
    private KeyState[] _keys;
    private Func<int, KeyState> _getKey;
    private LuaRuntime _runtime;
    private World _world;
    private SystemGroup _simulation;
    private SystemGroup _presentation;
    private LuaScriptSystem _scripts;
    private string _directory;

    [SetUp]
    public void SetUp()
    {
        _handler = new FakeConsoleHandler(Width, Height);
        _console = new RenderConsole(_handler);
        _keys = new KeyState[256];
        _getKey = key => _keys[key];
        _runtime = new LuaRuntime(_console, key => _getKey(key));

        _world = new World();
        _simulation = new SystemGroup(_world);
        _presentation = new SystemGroup(_world);
        _scripts = _simulation.Add(new LuaScriptSystem(_runtime));
        _presentation.Add(new GlyphRenderSystem(_console, new Camera()));
        _presentation.Add(new LuaRenderSystem(_scripts));

        _directory = Path.Combine(Path.GetTempPath(), "console-engine-lua-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        _world.Dispose();
        _runtime.Dispose();
        Directory.Delete(_directory, true);
    }

    //**********************************************************
    //** component fields:
    //**********************************************************

    [Test]
    public void Update_WhenScriptMovesSelf_MovesPositionByDeltaInSeconds()
    {
        var mover = _world.Create(new Position(new Vector2(1, 2)), Load("""
            local Mover = {}
            function Mover.on_update(self, dt) self.x = self.x + 4 * dt end
            return Mover
            """).Component);

        RunFrames(2);

        Assert.That(_world.Get<Position>(mover).Value, Is.EqualTo(new Vector2(5, 2)));
    }

    [Test]
    public void Update_FirstTime_RunsOnStartOnceWithComponentFieldsSet()
    {
        var counter = _world.Create(new Position(new Vector2(3, 0)), Load("""
            local Counter = {}
            function Counter.on_start(self) self.y = self.x * 2 end
            function Counter.on_update(self) self.x = self.x + 1 end
            return Counter
            """).Component);

        RunFrames(2);

        Assert.That(_world.Get<Position>(counter).Value, Is.EqualTo(new Vector2(5, 6)));
    }

    [Test]
    public void Update_WithVelocity_ExposesAndWritesVxVy()
    {
        var steering = _world.Create(new Position(), new Velocity(new Vector2(2, 3)), Load("""
            return { on_update = function(self) self.vx, self.vy = self.vy, -self.vx end }
            """).Component);

        RunFrames(1);

        Assert.That(_world.Get<Velocity>(steering).Value, Is.EqualTo(new Vector2(3, -2)));
    }

    [Test]
    public void Update_WithMovementSystem_EngineMovesWhatTheScriptSteers()
    {
        _simulation.Add(new MovementSystem());
        var rocket = _world.Create(new Position(), new Velocity(), Load("""
            return { on_update = function(self) self.vx = 2 end }
            """).Component);

        RunFrames(2);

        Assert.That(_world.Get<Position>(rocket).Value.X, Is.EqualTo(2));
    }

    [Test]
    public void Update_WhenScriptSetsGlyph_CopiesItAndGlyphRenderSystemDrawsIt()
    {
        var blinker = _world.Create(new Position(new Vector2(4, 2)), new Glyph('?', ConsoleColor.White), Load("""
            return {
                on_update = function(self)
                    self.char = "#"
                    self.color = colors.Red
                    self.background = colors.Blue
                end
            }
            """).Component);

        RunFrames(1);

        var glyph = _world.Get<Glyph>(blinker);
        var pixel = _handler.PixelAt(4, 2);
        Assert.Multiple(() =>
        {
            Assert.That(glyph.Character, Is.EqualTo('#'));
            Assert.That(glyph.Foreground, Is.EqualTo(ConsoleColor.Red));
            Assert.That(glyph.Background, Is.EqualTo(ConsoleColor.Blue));
            Assert.That(pixel.Char, Is.EqualTo('#'));
            Assert.That(pixel.ForegroundColor, Is.EqualTo(ConsoleColor.Red));
        });
    }

    [Test]
    public void Update_WhenScriptReadsGlyph_SeesComponentValues()
    {
        var reader = _world.Create(new Position(), new Glyph('Ø', ConsoleColor.Green), Load("""
            return {
                on_update = function(self)
                    if self.char == "Ø" and self.color == colors.Green then self.x = 1 end
                end
            }
            """).Component);

        RunFrames(1);

        Assert.That(_world.Get<Position>(reader).Value.X, Is.EqualTo(1));
    }

    [Test]
    public void Update_WhenScriptSetsCharAsCodePoint_CopiesThatCharacter()
    {
        var block = _world.Create(new Glyph(' ', ConsoleColor.White), Load("""
            return { on_update = function(self) self.char = utf8.codepoint("█") end }
            """).Component);

        RunFrames(1);

        Assert.That(_world.Get<Glyph>(block).Character, Is.EqualTo('█'));
    }

    [Test]
    public void Data_ForTwoEntities_IsSeparatePerEntityAndModuleFunctionsAreMethods()
    {
        var script = Load("""
            local Counter = {}
            function Counter.step(self) self.data.count = (self.data.count or 0) + 1 end
            function Counter.on_update(self)
                self:step()
                self.x = self.data.count
            end
            return Counter
            """);
        var first = _world.Create(new Position(), script.Component);
        var second = _world.Create(new Position(), script.Component);

        RunFrames(2);
        _world.Destroy(second);
        var third = _world.Create(new Position(), script.Component);
        RunFrames(1);

        Assert.Multiple(() =>
        {
            Assert.That(_world.Get<Position>(first).Value.X, Is.EqualTo(3));
            Assert.That(_world.Get<Position>(third).Value.X, Is.EqualTo(1), "reused id gets a fresh self");
        });
    }

    [Test]
    public void Update_WhenComponentRemoved_RemovesItsFieldsFromSelf()
    {
        var entity = _world.Create(new Position(), new Velocity(new Vector2(1, 1)), Load("""
            return { on_update = function(self) self.x = self.vx == nil and 7 or 1 end }
            """).Component);
        RunFrames(1);

        _world.Remove<Velocity>(entity);
        RunFrames(1);

        Assert.That(_world.Get<Position>(entity).Value.X, Is.EqualTo(7));
    }

    //**********************************************************
    //** errors:
    //**********************************************************

    [Test]
    public void Update_WhenScriptWritesUnknownField_FaultsWithHint()
    {
        var typo = _world.Create(new Position(), Load("""
            return { on_update = function(self) self.X = 5 end }
            """).Component);

        RunFrames(1);

        Assert.That(_scripts.GetError(typo),
            Does.Contain("unknown field 'X' on an entity, put script data in self.data"));
    }

    [Test]
    public void Update_WhenScriptWritesFieldOfMissingComponent_FaultsNamingTheComponent()
    {
        var walker = _world.Create(new Position(), Load("""
            return { on_update = function(self) self.vx = 1 end }
            """).Component);

        RunFrames(1);

        Assert.That(_scripts.GetError(walker), Does.Contain("self.vx needs a Velocity component on the entity"));
    }

    [Test]
    public void Update_WhenScriptStoresWrongType_FaultsTagsEntityAndKeepsComponents()
    {
        var broken = _world.Create(new Position(new Vector2(1, 1)), Load("""
            return {
                on_update = function(self)
                    self.y = 7
                    self.x = "left"
                end
            }
            """).Component);

        RunFrames(1);

        Assert.Multiple(() =>
        {
            Assert.That(_scripts.GetError(broken), Is.EqualTo("self.x must be a number"));
            Assert.That(_world.Has<ScriptFaulted>(broken), Is.True);
            Assert.That(_world.Get<Position>(broken).Value, Is.EqualTo(new Vector2(1, 1)));
        });
    }

    [Test]
    public void Update_WhenScriptErrors_FaultsWithFileAndLineAndStopsCalling()
    {
        var broken = _world.Create(new Position(), Load("""
            local Broken = {}
            function Broken.on_update(self)
                self.x = self.x + 1
                error("boom")
            end
            return Broken
            """).Component);

        RunFrames(3);

        Assert.Multiple(() =>
        {
            Assert.That(_scripts.GetError(broken), Does.Contain("script.lua:4: boom"));
            Assert.That(_world.Get<Position>(broken).Value.X, Is.EqualTo(0));
        });
    }

    [Test]
    public void Update_WhenFaultTagRemovedByHand_RunsAgainAndClearsError()
    {
        var flaky = _world.Create(new Position(), Load("""
            return {
                on_update = function(self)
                    self.data.calls = (self.data.calls or 0) + 1
                    if self.data.calls == 1 then error("first call fails") end
                    self.x = self.data.calls
                end
            }
            """).Component);
        RunFrames(1);

        _world.Remove<ScriptFaulted>(flaky);
        RunFrames(1);

        Assert.Multiple(() =>
        {
            Assert.That(_scripts.GetError(flaky), Is.Null);
            Assert.That(_world.Get<Position>(flaky).Value.X, Is.EqualTo(2));
        });
    }

    [Test]
    public void Update_WhenHostFunctionThrows_FaultsWithExceptionMessage()
    {
        _getKey = _ => throw new InvalidOperationException("Input handler not set");
        var reader = _world.Create(new Position(), Load("""
            return { on_update = function(self) input.held(keys.LEFT) end }
            """).Component);

        RunFrames(1);

        Assert.That(_scripts.GetError(reader), Is.EqualTo("InvalidOperationException: Input handler not set"));
    }

    //**********************************************************
    //** rendering:
    //**********************************************************

    [Test]
    public void Render_DrawsScriptOutputButDoesNotCopyFieldsBack()
    {
        var hud = _world.Create(new Position(new Vector2(2, 3)), new Glyph('@', ConsoleColor.White), Load("""
            return {
                on_render = function(self)
                    console.text(0, 0, "Bær " .. 42)
                    self.x = 15
                end
            }
            """).Component);

        RunFrames(1);

        Assert.Multiple(() =>
        {
            Assert.That(Row(0, 6), Is.EqualTo("Bær 42"));
            Assert.That(_handler.PixelAt(2, 3).Char, Is.EqualTo('@'));
            Assert.That(_world.Get<Position>(hud).Value.X, Is.EqualTo(2));
        });
    }

    [Test]
    public void Render_WhenScriptDrawsStringAndCodePoint_WritesCharactersWithColors()
    {
        _world.Create(Load("""
            return {
                on_render = function(self)
                    console.draw(3.7, 2, "@x", colors.Red, colors.Blue)
                    console.draw(1, 1, utf8.codepoint("█"))
                end
            }
            """).Component);

        RunFrames(1);

        Assert.Multiple(() =>
        {
            Assert.That(_handler.PixelAt(3, 2).Char, Is.EqualTo('@'));
            Assert.That(_handler.PixelAt(3, 2).ForegroundColor, Is.EqualTo(ConsoleColor.Red));
            Assert.That(_handler.PixelAt(3, 2).BackgroundColor, Is.EqualTo(ConsoleColor.Blue));
            Assert.That(_handler.PixelAt(1, 1).Char, Is.EqualTo('█'));
            Assert.That(_handler.PixelAt(1, 1).ForegroundColor, Is.EqualTo(ConsoleColor.White));
        });
    }

    //**********************************************************
    //** engine API:
    //**********************************************************

    [Test]
    public void Update_WhenKeyIsHeld_InputReturnsKeyState()
    {
        _keys[(int)Key.LEFT] = new KeyState { Held = true, Pressed = true };
        var reader = _world.Create(new Position(), Load("""
            return {
                on_update = function(self)
                    if input.held(keys.LEFT) and input.pressed(keys.LEFT) then self.x = 1 end
                    if input.held(keys.RIGHT) or input.held(1000) then self.y = 1 end
                end
            }
            """).Component);

        RunFrames(1);

        Assert.That(_world.Get<Position>(reader).Value, Is.EqualTo(new Vector2(1, 0)));
    }

    [Test]
    public void Runtime_Always_ExposesConsoleSizeKeysAndColors()
    {
        var reader = _world.Create(new Position(), new Glyph(' ', ConsoleColor.White), Load("""
            return {
                on_update = function(self)
                    self.x, self.y = console.width, console.height
                    self.char, self.color = keys.LEFT, colors.Red
                end
            }
            """).Component);

        RunFrames(1);

        Assert.Multiple(() =>
        {
            Assert.That(_world.Get<Position>(reader).Value, Is.EqualTo(new Vector2(Width, Height)));
            Assert.That(_world.Get<Glyph>(reader).Character, Is.EqualTo((char)Key.LEFT));
            Assert.That(_world.Get<Glyph>(reader).Foreground, Is.EqualTo(ConsoleColor.Red));
        });
    }

    //**********************************************************
    //** loading and reloading:
    //**********************************************************

    [Test]
    public void Reload_WhenFunctionChanged_RunningEntitiesUseNewFunctionAndKeepData()
    {
        var script = Load("""
            return {
                on_update = function(self)
                    self.data.ticks = (self.data.ticks or 0) + 1
                    self.x = self.data.ticks
                end
            }
            """);
        var mover = _world.Create(new Position(), script.Component);
        RunFrames(1);

        Write("""
            return {
                on_update = function(self)
                    self.data.ticks = self.data.ticks + 10
                    self.x = self.data.ticks
                end
            }
            """);
        script.Reload();
        RunFrames(1);

        Assert.Multiple(() =>
        {
            Assert.That(_world.Get<Position>(mover).Value.X, Is.EqualTo(11));
            Assert.That(script.Version, Is.EqualTo(2));
        });
    }

    [Test]
    public void Reload_AfterFault_UntagsEntityAndRunsItAgain()
    {
        var script = Load("""
            return { on_update = function(self) error("boom") end }
            """);
        var mover = _world.Create(new Position(), script.Component);
        RunFrames(1);

        Write("""
            return { on_update = function(self) self.x = self.x + 1 end }
            """);
        script.Reload();
        RunFrames(2);

        Assert.Multiple(() =>
        {
            Assert.That(_world.Has<ScriptFaulted>(mover), Is.False);
            Assert.That(_scripts.GetError(mover), Is.Null);
            Assert.That(_world.Get<Position>(mover).Value.X, Is.EqualTo(1), "untagged on frame 1, runs on frame 2");
        });
    }

    [TestCase("io")]
    [TestCase("os")]
    [TestCase("debug")]
    [TestCase("package")]
    [TestCase("require")]
    [TestCase("load")]
    [TestCase("loadfile")]
    [TestCase("dofile")]
    [TestCase("collectgarbage")]
    public void Sandbox_UnsafeGlobal_IsNotAvailable(string name)
    {
        var script = Load($$"""
            return { on_update = function(self) self.x = _G["{{name}}"] == nil and 1 or 0 end }
            """);
        var mover = _world.Create(new Position(), script.Component);

        RunFrames(1);

        Assert.That(_world.Get<Position>(mover).Value.X, Is.EqualTo(1));
    }

    [Test]
    public void Sandbox_SafeLibraries_AreAvailable()
    {
        var script = Load("""
            return {
                on_update = function(self)
                    local co = coroutine.wrap(function() coroutine.yield(2) end)
                    self.x = math.floor(1.5) + co() + #string.rep("a", 3) + #table.pack(1, 2) + utf8.len("ab")
                        + select("#", pcall(tostring, 1))
                end
            }
            """);
        var mover = _world.Create(new Position(), script.Component);

        RunFrames(1);

        Assert.That(_world.Get<Position>(mover).Value.X, Is.EqualTo(1 + 2 + 3 + 2 + 2 + 2));
    }

    [Test]
    public void Reload_AfterOnStartFault_RunsOnStartAgain()
    {
        var script = Load("""
            return {
                on_start = function(self) error("boom") end,
                on_update = function(self) self.x = self.data.speed end
            }
            """);
        var mover = _world.Create(new Position(), script.Component);
        RunFrames(1);

        Write("""
            return {
                on_start = function(self) self.data.speed = 5 end,
                on_update = function(self) self.x = self.data.speed end
            }
            """);
        script.Reload();
        RunFrames(2);

        Assert.Multiple(() =>
        {
            Assert.That(_world.Has<ScriptFaulted>(mover), Is.False);
            Assert.That(_world.Get<Position>(mover).Value.X, Is.EqualTo(5));
        });
    }

    [Test]
    public void Reload_WhenFieldRemoved_RemovesItFromModule()
    {
        var script = Load("""
            return {
                speed = 3,
                on_update = function(self) self.x = self.x + 1 end
            }
            """);
        var mover = _world.Create(new Position(), script.Component);
        RunFrames(1);

        Write("return {}");
        script.Reload();
        RunFrames(2);

        Assert.That(_world.Get<Position>(mover).Value.X, Is.EqualTo(1), "removed on_update no longer runs");

        Write("""
            return { on_update = function(self) self.x = self.speed or -1 end }
            """);
        script.Reload();
        RunFrames(1);

        Assert.That(_world.Get<Position>(mover).Value.X, Is.EqualTo(-1), "removed field speed is gone");
    }

    [Test]
    public void Reload_WhenNewVersionHasSyntaxError_ThrowsAndKeepsPreviousVersion()
    {
        var script = Load("""
            return { on_update = function(self) self.x = 1 end }
            """);
        var mover = _world.Create(new Position(), script.Component);

        Write("return { on_update = function(self) self.x = end }");
        var exception = Assert.Throws<LuaScriptException>(() => script.Reload());
        RunFrames(1);

        Assert.Multiple(() =>
        {
            // Lua shortens long chunk names to "...<end of path>", so only the file name is reliable here.
            Assert.That(exception.Message, Does.StartWith($"Lua script '{ScriptPath}': "));
            Assert.That(exception.Message, Does.Contain("script.lua:1: unexpected symbol near 'end'"));
            Assert.That(_world.Get<Position>(mover).Value.X, Is.EqualTo(1));
            Assert.That(script.Version, Is.EqualTo(1));
        });
    }

    [Test]
    public void Load_WhenScriptDoesNotReturnTable_Throws()
    {
        var exception = Assert.Throws<LuaScriptException>(() => Load("local x = 1"));

        Assert.That(exception.Message, Is.EqualTo($"Lua script '{ScriptPath}': the script must return a table"));
    }

    [Test]
    public void Load_WhenFileIsMissing_Throws()
    {
        Assert.Throws<LuaScriptException>(() => _runtime.Load(Path.Combine(_directory, "missing.lua")));
    }

    [Test]
    public void Load_WhenFileStartsWithByteOrderMark_Loads()
    {
        File.WriteAllText(ScriptPath, "return { on_update = function(self) self.x = 1 end }",
            new System.Text.UTF8Encoding(true));
        var mover = _world.Create(new Position(), _runtime.Load(ScriptPath).Component);

        RunFrames(1);

        Assert.That(_world.Get<Position>(mover).Value.X, Is.EqualTo(1));
    }

    [Test]
    public void ReloadAll_WhenOneScriptFails_ReturnsItsFailure()
    {
        Load("return {}");
        Write("syntax error here");

        var failures = _runtime.ReloadAll();

        Assert.That(failures, Has.Length.EqualTo(1));
    }

    //**********************************************************
    //** performance:
    //**********************************************************

    [Test]
    public void Frame_InSteadyState_DoesNotAllocate()
    {
        _keys[(int)Key.LEFT] = new KeyState { Held = true };
        var script = Load("""
            local Ship = {}
            local HUD = "Score: 10"
            function Ship.on_update(self, dt)
                if input.held(keys.LEFT) then self.x = self.x - dt end
                self.char = "@"
                self.data.ticks = (self.data.ticks or 0) + 1
            end
            function Ship.on_render(self)
                console.text(0, 0, HUD, colors.Yellow)
            end
            return Ship
            """);
        for (var i = 0; i < 10; i++)
            _world.Create(new Position(new Vector2(10, 5)), new Velocity(), new Glyph(' ', ConsoleColor.White),
                script.Component);

        const int frames = 1000;
        Step(frames);
        var before = GC.GetAllocatedBytesForCurrentThread();
        Step(frames);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Tiered JIT still allocates a few dozen bytes per thousand frames, also for systems without Lua. Any real
        // per-frame allocation is at least 24 bytes per frame.
        Assert.That(allocated, Is.LessThan(frames));
    }

    //**********************************************************
    //** helpers:
    //**********************************************************

    private string ScriptPath => Path.Combine(_directory, "script.lua");

    private void Write(string source) => File.WriteAllText(ScriptPath, source);

    private LuaScript Load(string source)
    {
        Write(source);
        return _runtime.Load(ScriptPath);
    }

    /// <summary>Runs frames, then hands the frame buffer to <see cref="FakeConsoleHandler.LastFrame"/>.</summary>
    private void RunFrames(int frames)
    {
        Step(frames);
        _console.Display();
    }

    /// <summary>Runs frames without <c>Display</c>, which copies the buffer in <see cref="FakeConsoleHandler"/>.</summary>
    private void Step(int frames)
    {
        for (var frame = 0; frame < frames; frame++)
        {
            _console.Clear();
            _simulation.Update(Frame);
            _presentation.Update(Frame);
        }
    }

    private string Row(int y, int length)
    {
        var chars = new char[length];
        for (var x = 0; x < length; x++)
            chars[x] = _handler.PixelAt(x, y).Char;
        return new string(chars);
    }
}
