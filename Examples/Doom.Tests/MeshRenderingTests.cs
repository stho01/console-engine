using System;
using System.Linq;
using ConsoleEngine.Rendering3D;
using Doom.Graphics;
using Doom.Play;
using Doom.Rendering;
using Doom.Rendering.Meshes;
using Doom.Tests.Fakes;
using NUnit.Framework;

namespace Doom.Tests;

public class MeshRenderingTests
{
    /// <summary>The test map's sectors are lit 160: Doom's light table 7 of 32 right in front.</summary>
    private const float SectorBrightness = 25 / 32f;

    /// <summary>Where the mesh view's fog is black: (32 + 4) light tables of 96 units.</summary>
    private const float FogEnd = 36 * 96;

    /// <summary>The door faces along x, so it is drawn a little lighter (Doom's fake contrast).</summary>
    private const float DoorShade = 1.1f;

    private Level _level;
    private MeshRenderer _renderer;

    [TearDown]
    public void DisposeLevel()
    {
        _level?.Dispose();
        _renderer?.Dispose();
    }

    private (Level Level, MeshRenderer Renderer, FrameBuffer Frame) Setup(Layout layout = null)
    {
        layout ??= Layout.Standard;
        var (_, palette, textures, _) = TestWad.Load();
        var level = _level = TestWad.CreateLevel();
        var frame = new FrameBuffer(layout.ViewWidth, layout.ViewHeight);
        _renderer = new MeshRenderer(frame, palette, textures, level.Map);
        return (level, _renderer, frame);
    }

    private static Camera CameraOf(Level level)
    {
        ref var body = ref level.PlayerBody;
        return new Camera(body.X, body.Y, level.Player.ViewZ, body.Angle);
    }

    /// <summary>A texel at <paramref name="distance"/> along the view in a test-map sector.</summary>
    private static int Lit(byte color, float distance, float shade = 1) =>
        (int)MathF.Round(color * SectorBrightness * shade * (1 - distance / FogEnd));

    /// <summary>Triangles that are not padding (padding repeats one corner).</summary>
    private static int RealTriangles(Mesh mesh)
    {
        if (mesh == null)
            return 0;

        var count = 0;
        for (var t = 0; t < mesh.TriangleCount; t++)
        {
            if (mesh.Indices[t * 3] != mesh.Indices[t * 3 + 1])
                count++;
        }

        return count;
    }

    [Test]
    public void Build_TestMap_SubsectorPolygonsCoverTheRooms()
    {
        using var level = TestWad.CreateLevel();

        var polygons = SubsectorPolygons.Build(level.Map);

        var areas = polygons.Select(p => ConvexPolygon.DoubleArea(p.ToList()) / 2).ToArray();
        Assert.That(areas, Is.EqualTo(new[] { 256f * 256, 16f * 64, 128f * 256 }).Within(0.5f));
    }

    [Test]
    public void Build_TestMap_SkyCeilingIsItsOwnUnlitMesh()
    {
        var (level, renderer, _) = Setup();

        var sky = renderer.LevelMeshes.Subsectors[2].Sky;

        Assert.That(sky, Is.Not.Null);
        Assert.That(sky.IsUnlit, Is.True);
        Assert.That(sky.TriangleColors, Has.All.EqualTo(LevelMeshes.SkyColor));
        Assert.That(renderer.LevelMeshes.Subsectors[0].Sky, Is.Null);
        Assert.That(level.Map.Sectors, Has.Length.EqualTo(3));
    }

    [Test]
    public void Render_FacingClosedDoor_DrawsCeilingWallAndFloor()
    {
        var (level, renderer, frame) = Setup();

        renderer.Render(level, CameraOf(level), -1);

        var middle = frame.Width / 2;
        // Distances along the view: the ceiling (87 above the eye) at the top row, the door at 192, the floor (41 below)
        // at the bottom row; a row is 1/80 of a unit of slope.
        Assert.That(frame.Get(middle, 0), Is.EqualTo(Lit(TestWad.CeilingColor, 87 / (49.5f / 80))).Within(2));
        Assert.That(frame.Get(middle, frame.Height / 2), Is.EqualTo(Lit(TestWad.WallColor, 192, DoorShade)).Within(2));
        Assert.That(frame.Get(middle, frame.Height - 1), Is.EqualTo(Lit(TestWad.FloorColor, 41 / (49.5f / 80))).Within(2));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Render_EveryDirection_LeavesNoHole(bool high)
    {
        var (level, renderer, frame) = Setup(high ? Layout.High : Layout.Standard);
        var body = level.PlayerBody;

        for (var turn = 0; turn < 16; turn++)
        {
            renderer.Render(level, new Camera(body.X, body.Y, level.Player.ViewZ, turn * MathF.PI / 8), -1);
            for (var i = 0; i < frame.Pixels.Length; i++)
                Assert.That(frame.Pixels[i], Is.Not.Zero, $"turn {turn}, pixel {i % frame.Width}, {i / frame.Width}");
        }
    }

    [Test]
    public void Render_UnderSkyCeiling_DrawsTheSkyPicture()
    {
        var (level, renderer, frame) = Setup();
        var (_, _, textures, _) = TestWad.Load();
        var sky = textures.FindTexture("STARTAN");

        renderer.Render(level, new Camera(336, 10, 41, MathF.PI / 2), sky);

        Assert.That(frame.Get(frame.Width / 2, 0), Is.EqualTo(TestWad.WallColor));
    }

    [Test]
    public void Render_ThingInView_DrawsItsSprite()
    {
        var (level, renderer, frame) = Setup();

        renderer.Render(level, new Camera(10, 128, 41, 0), -1);

        // The stimpack (4 × 4, colour 200) at x = 96 on the floor, 86 units ahead: rows 84 to 88.
        Assert.That(frame.Get(frame.Width / 2, 86), Is.EqualTo(Lit(200, 86)).Within(2));
    }

    [Test]
    public void Render_FixedLight_UsesTheLightTable()
    {
        var (level, renderer, frame) = Setup();

        renderer.Render(level, CameraOf(level), -1, fixedLight: 0);

        Assert.That(frame.Get(frame.Width / 2, frame.Height / 2), Is.EqualTo(TestWad.WallColor * DoorShade).Within(2));
    }

    [Test]
    public void Update_DoorOpened_RebuildsTheWallsAroundIt()
    {
        var (level, renderer, _) = Setup();
        var walls = renderer.LevelMeshes.Subsectors[0];
        var closed = RealTriangles(walls.Walls);

        level.Map.Sectors[TestWad.DoorSector].CeilingHeight = 128;
        renderer.LevelMeshes.Update();

        Assert.That(RealTriangles(walls.Walls), Is.LessThan(closed));
        Assert.That(RealTriangles(renderer.LevelMeshes.Subsectors[TestWad.DoorSector].Walls), Is.GreaterThan(0));
    }

    [Test]
    public void Render_SeenSectors_AreMarkedForTheAutomap()
    {
        var (level, renderer, _) = Setup();

        renderer.Render(level, CameraOf(level), -1);

        Assert.That(level.Map.Lines[4].Mapped, Is.True);
    }

    [Test]
    public void Render_SteadyState_DoesNotAllocate()
    {
        var (level, renderer, _) = Setup();
        var camera = new Camera(10, 128, 41, 0);
        renderer.Render(level, camera, 0);
        renderer.Render(level, camera, 0);

        var before = GC.GetAllocatedBytesForCurrentThread();
        renderer.Render(level, camera, 0);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.Zero);
    }

    [Test]
    public void Nearest_PaletteExtremesAndGreys_AreTheClosestColours()
    {
        var (_, palette, _, _) = TestWad.Load();
        var quantizer = new PaletteQuantizer(palette);

        Assert.That(quantizer.Nearest(0x000000), Is.LessThanOrEqualTo(2));
        Assert.That(quantizer.Nearest(0xFFFFFF), Is.GreaterThanOrEqualTo(253));
        Assert.That(quantizer.Nearest(0x646464), Is.EqualTo(100).Within(2));
    }

    [Test]
    public void Find_FacingClosedDoor_RoomBehindIsNotVisible()
    {
        var (level, renderer, _) = Setup();
        var visibility = new Visibility(Layout.Standard.ViewWidth, level.Map.Sectors.Length,
            renderer.LevelMeshes.Polygons);

        visibility.Find(level.Map, CameraOf(level));

        Assert.That(visibility.VisibleSubsectors.ToArray(), Is.EqualTo(new[] { 0 }));
        Assert.That(visibility.IsSectorVisible(2), Is.False);
    }

    [Test]
    public void Find_DoorOpen_RoomBehindIsVisible()
    {
        var (level, renderer, _) = Setup();
        var visibility = new Visibility(Layout.Standard.ViewWidth, level.Map.Sectors.Length,
            renderer.LevelMeshes.Polygons);
        level.Map.Sectors[TestWad.DoorSector].CeilingHeight = 128;

        visibility.Find(level.Map, CameraOf(level));

        Assert.That(visibility.VisibleSubsectors.ToArray(), Is.EquivalentTo(new[] { 0, 1, 2 }));
        Assert.That(visibility.IsSectorVisible(2), Is.True);
    }

    [Test]
    public void Get_SpritePicture_IsOneTexturedQuad()
    {
        var (_, palette, textures, _) = TestWad.Load();
        var sprites = new SpriteMeshes(new TextureCache(palette, textures));
        var picture = new Picture(4, 4, 2, 4);
        Array.Fill(picture.Opaque, true);

        var mesh = sprites.Get(picture, mirrored: false);
        var mirrored = sprites.Get(picture, mirrored: true);

        Assert.That(mesh.TriangleCount, Is.EqualTo(2));
        Assert.That(mesh.TriangleTextures, Has.All.Not.Null);
        Assert.That(mesh.TextureCoordinates[0].X, Is.EqualTo(1 - mirrored.TextureCoordinates[0].X));
        Assert.That(sprites.Get(picture, mirrored: false, fullBright: true).IsUnlit, Is.True);
    }

    [Test]
    public void Build_TestMap_WallsAndFlatsAreTexturedQuadsAndFans()
    {
        var (_, renderer, _) = Setup();

        var roomA = renderer.LevelMeshes.Subsectors[0];

        // Room A: a square floor and ceiling (two triangles each), five solid walls and the door's upper part.
        Assert.That(RealTriangles(roomA.Flats), Is.EqualTo(4));
        Assert.That(RealTriangles(roomA.Walls), Is.EqualTo(12));
        Assert.That(roomA.Walls.TriangleTextures, Has.Some.Not.Null);
    }
}
