using System;
using System.IO;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Objects;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Scripting;
using ConsoleEngine.Tests.Fakes;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Scripting;

public class LuaBehaviourTests
{
    private const int Width = 20;
    private const int Height = 10;
    private static readonly TimeSpan Frame = TimeSpan.FromSeconds(0.5);

    private FakeConsoleHandler _handler;
    private RenderConsole _console;
    private KeyState[] _keys;
    private Func<int, KeyState> _getKey;
    private LuaRuntime _runtime;
    private string _directory;

    [SetUp]
    public void SetUp()
    {
        _handler = new FakeConsoleHandler(Width, Height);
        _console = new RenderConsole(_handler);
        _keys = new KeyState[256];
        _getKey = key => _keys[key];
        _runtime = new LuaRuntime(_console, key => _getKey(key));
        _directory = Path.Combine(Path.GetTempPath(), "console-engine-lua-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        _runtime.Dispose();
        Directory.Delete(_directory, true);
    }

    //**********************************************************
    //** engine fields:
    //**********************************************************

    [Test]
    public void Update_WhenScriptMovesSelf_MovesGameObjectByDeltaInSeconds()
    {
        var mover = Create("""
            local Mover = {}
            function Mover.on_update(self, dt) self.x = self.x + 4 * dt end
            return Mover
            """, new Vector2(1, 2));

        mover.Update(Frame);
        mover.Update(Frame);

        Assert.That(mover.Position, Is.EqualTo(new Vector2(5, 2)));
    }

    [Test]
    public void Update_FirstTime_RunsOnStartOnceWithEngineFieldsSet()
    {
        var counter = Create("""
            local Counter = {}
            function Counter.on_start(self) self.y = self.x * 2 end
            function Counter.on_update(self) self.x = self.x + 1 end
            return Counter
            """, new Vector2(3, 0));

        counter.Update(Frame);
        counter.Update(Frame);

        Assert.That(counter.Position, Is.EqualTo(new Vector2(5, 6)));
    }

    [Test]
    public void Update_WhenScriptSetsAppearance_CopiesItToGameObject()
    {
        var blinker = Create("""
            return {
                on_update = function(self)
                    self.char = "#"
                    self.color = colors.Red
                    self.background = colors.Blue
                    self.visible = false
                end
            }
            """);

        blinker.Update(Frame);

        Assert.Multiple(() =>
        {
            Assert.That(blinker.Char, Is.EqualTo('#'));
            Assert.That(blinker.Color, Is.EqualTo(ConsoleColor.Red));
            Assert.That(blinker.Background, Is.EqualTo(ConsoleColor.Blue));
            Assert.That(blinker.Visible, Is.False);
        });
    }

    [Test]
    public void Update_WhenScriptReadsAppearance_SeesGameObjectValues()
    {
        var reader = Create("""
            return {
                on_update = function(self)
                    if self.char == "Ø" and self.color == colors.Green and self.visible then self.x = 1 end
                end
            }
            """);
        reader.Char = 'Ø';
        reader.Color = ConsoleColor.Green;

        reader.Update(Frame);

        Assert.That(reader.Position.X, Is.EqualTo(1));
    }

    [Test]
    public void Update_WhenScriptSetsCharAsCodePoint_CopiesThatCharacter()
    {
        var block = Create("""
            return { on_update = function(self) self.char = utf8.codepoint("█") end }
            """);

        block.Update(Frame);

        Assert.That(block.Char, Is.EqualTo('█'));
    }

    [Test]
    public void Data_ForTwoObjects_IsSeparatePerObject()
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
        var first = new GameObject { Behaviour = script.CreateBehaviour() };
        var second = new GameObject { Behaviour = script.CreateBehaviour() };

        first.Update(Frame);
        first.Update(Frame);
        second.Update(Frame);

        Assert.Multiple(() =>
        {
            Assert.That(first.Position.X, Is.EqualTo(2));
            Assert.That(second.Position.X, Is.EqualTo(1));
        });
    }

    [Test]
    public void Update_WhenScriptWritesUnknownField_FaultsWithHint()
    {
        var typo = Create("""
            return { on_update = function(self) self.X = 5 end }
            """);

        typo.Update(Frame);

        Assert.That(Behaviour(typo).Error,
            Does.Contain("unknown field 'X' on a game object, put script data in self.data"));
    }

    [Test]
    public void Update_WhenScriptStoresWrongType_FaultsAndKeepsLastGoodState()
    {
        var broken = Create("""
            return {
                on_update = function(self)
                    self.y = 7
                    self.x = "left"
                end
            }
            """, new Vector2(1, 1));

        broken.Update(Frame);

        Assert.Multiple(() =>
        {
            Assert.That(Behaviour(broken).Error, Is.EqualTo("self.x must be a number"));
            Assert.That(broken.Position, Is.EqualTo(new Vector2(1, 1)));
        });
    }

    //**********************************************************
    //** rendering:
    //**********************************************************

    [Test]
    public void Render_DrawsGameObjectAndScriptDrawingButDoesNotCopyFieldsBack()
    {
        var hud = Create("""
            return {
                on_render = function(self)
                    console.text(0, 0, "Bær " .. 42)
                    self.x = 15
                end
            }
            """, new Vector2(2, 3));
        hud.Char = '@';

        hud.Render(_console);
        _console.Display();

        Assert.Multiple(() =>
        {
            Assert.That(Row(0, 6), Is.EqualTo("Bær 42"));
            Assert.That(_handler.PixelAt(2, 3).Char, Is.EqualTo('@'));
            Assert.That(hud.Position.X, Is.EqualTo(2));
        });
    }

    [Test]
    public void Render_WhenScriptDrawsString_WritesFirstCharacterWithColors()
    {
        var drawer = Create("""
            return { on_render = function(self) console.draw(3.7, 2, "@x", colors.Red, colors.Blue) end }
            """, new Vector2(-1, -1));

        drawer.Render(_console);
        _console.Display();

        var pixel = _handler.PixelAt(3, 2);
        Assert.Multiple(() =>
        {
            Assert.That(pixel.Char, Is.EqualTo('@'));
            Assert.That(pixel.ForegroundColor, Is.EqualTo(ConsoleColor.Red));
            Assert.That(pixel.BackgroundColor, Is.EqualTo(ConsoleColor.Blue));
        });
    }

    [Test]
    public void Render_WhenScriptDrawsCodePoint_WritesThatCharacterWithDefaultColors()
    {
        var drawer = Create("""
            return { on_render = function(self) console.draw(1, 1, utf8.codepoint("█")) end }
            """, new Vector2(-1, -1));

        drawer.Render(_console);
        _console.Display();

        var pixel = _handler.PixelAt(1, 1);
        Assert.Multiple(() =>
        {
            Assert.That(pixel.Char, Is.EqualTo('█'));
            Assert.That(pixel.ForegroundColor, Is.EqualTo(ConsoleColor.White));
            Assert.That(pixel.BackgroundColor, Is.EqualTo(ConsoleColor.Black));
        });
    }

    //**********************************************************
    //** engine API:
    //**********************************************************

    [Test]
    public void Update_WhenKeyIsHeld_InputReturnsKeyState()
    {
        _keys[(int)Key.LEFT] = new KeyState { Held = true, Pressed = true };
        var reader = Create("""
            return {
                on_update = function(self)
                    if input.held(keys.LEFT) and input.pressed(keys.LEFT) then self.x = 1 end
                    if input.held(keys.RIGHT) or input.held(1000) then self.y = 1 end
                end
            }
            """);

        reader.Update(Frame);

        Assert.That(reader.Position, Is.EqualTo(new Vector2(1, 0)));
    }

    [Test]
    public void Runtime_Always_ExposesConsoleSizeKeysAndColors()
    {
        var reader = Create("""
            return {
                on_update = function(self)
                    self.x, self.y = console.width, console.height
                    self.char, self.color = keys.LEFT, colors.Red
                end
            }
            """);

        reader.Update(Frame);

        Assert.Multiple(() =>
        {
            Assert.That(reader.Position, Is.EqualTo(new Vector2(Width, Height)));
            Assert.That(reader.Char, Is.EqualTo((char)Key.LEFT));
            Assert.That(reader.Color, Is.EqualTo(ConsoleColor.Red));
        });
    }

    //**********************************************************
    //** errors:
    //**********************************************************

    [Test]
    public void Update_WhenScriptErrors_FaultsWithFileAndLineAndStopsCalling()
    {
        var broken = Create("""
            local Broken = {}
            function Broken.on_update(self)
                self.data.calls = (self.data.calls or 0) + 1
                error("boom")
            end
            function Broken.on_render(self) self.data.rendered = true end
            return Broken
            """, new Vector2(4, 0));

        broken.Update(Frame);
        broken.Update(Frame);

        Assert.Multiple(() =>
        {
            Assert.That(Behaviour(broken).IsFaulted, Is.True);
            Assert.That(Behaviour(broken).Error, Does.Contain("script.lua:4: boom"));
            Assert.That(broken.Position.X, Is.EqualTo(4));
        });
    }

    [Test]
    public void Update_WhenHostFunctionThrows_FaultsWithExceptionMessage()
    {
        _getKey = _ => throw new InvalidOperationException("Input handler not set");
        var reader = Create("""
            return { on_update = function(self) input.held(keys.LEFT) end }
            """);

        reader.Update(Frame);

        Assert.That(Behaviour(reader).Error, Is.EqualTo("InvalidOperationException: Input handler not set"));
    }

    //**********************************************************
    //** loading and reloading:
    //**********************************************************

    [Test]
    public void Reload_WhenFunctionChanged_ExistingObjectsUseNewFunctionAndKeepData()
    {
        var script = Load("""
            return {
                on_update = function(self)
                    self.data.ticks = (self.data.ticks or 0) + 1
                    self.x = self.data.ticks
                end
            }
            """);
        var mover = new GameObject { Behaviour = script.CreateBehaviour() };
        mover.Update(Frame);

        Write("""
            return {
                on_update = function(self)
                    self.data.ticks = self.data.ticks + 10
                    self.x = self.data.ticks
                end
            }
            """);
        script.Reload();
        mover.Update(Frame);

        Assert.Multiple(() =>
        {
            Assert.That(mover.Position.X, Is.EqualTo(11));
            Assert.That(script.Version, Is.EqualTo(2));
        });
    }

    [Test]
    public void Reload_AfterFault_LetsBehaviourRunAgain()
    {
        var script = Load("""
            return { on_update = function(self) error("boom") end }
            """);
        var mover = new GameObject { Behaviour = script.CreateBehaviour() };
        mover.Update(Frame);

        Write("""
            return { on_update = function(self) self.x = 1 end }
            """);
        script.Reload();
        mover.Update(Frame);

        Assert.Multiple(() =>
        {
            Assert.That(Behaviour(mover).IsFaulted, Is.False);
            Assert.That(mover.Position.X, Is.EqualTo(1));
        });
    }

    [Test]
    public void Reload_WhenNewVersionHasSyntaxError_ThrowsAndKeepsPreviousVersion()
    {
        var script = Load("""
            return { on_update = function(self) self.x = 1 end }
            """);
        var mover = new GameObject { Behaviour = script.CreateBehaviour() };

        Write("return { on_update = function(self) self.x = end }");
        var exception = Assert.Throws<LuaScriptException>(() => script.Reload());
        mover.Update(Frame);

        Assert.Multiple(() =>
        {
            // Lua shortens long chunk names to "...<end of path>", so only the file name is reliable here.
            Assert.That(exception.Message, Does.StartWith($"Lua script '{ScriptPath}': "));
            Assert.That(exception.Message, Does.Contain("script.lua:1: unexpected symbol near 'end'"));
            Assert.That(mover.Position.X, Is.EqualTo(1));
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

        var mover = new GameObject { Behaviour = _runtime.Load(ScriptPath).CreateBehaviour() };
        mover.Update(Frame);

        Assert.That(mover.Position.X, Is.EqualTo(1));
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
    public void UpdateAndRender_InSteadyState_DoNotAllocate()
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
        var ships = new GameObject[10];
        for (var i = 0; i < ships.Length; i++)
            ships[i] = new GameObject { Position = new Vector2(10, 5), Behaviour = script.CreateBehaviour() };

        RunFrames(ships, 100);
        var before = GC.GetAllocatedBytesForCurrentThread();
        RunFrames(ships, 1000);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.Zero);
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

    private GameObject Create(string source, Vector2 position = default) =>
        new() { Position = position, Behaviour = Load(source).CreateBehaviour() };

    private static LuaBehaviour Behaviour(GameObject gameObject) => (LuaBehaviour)gameObject.Behaviour;

    private string Row(int y, int length)
    {
        var chars = new char[length];
        for (var x = 0; x < length; x++)
            chars[x] = _handler.PixelAt(x, y).Char;
        return new string(chars);
    }

    private void RunFrames(GameObject[] objects, int frames)
    {
        for (var frame = 0; frame < frames; frame++)
        for (var i = 0; i < objects.Length; i++)
        {
            objects[i].Update(TimeSpan.FromMilliseconds(16));
            objects[i].Render(_console);
        }
    }
}
