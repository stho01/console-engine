using System;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using ConsoleEngine.Rendering3D;
using ConsoleEngine.Rendering3D.OpenGL;

namespace ConsoleEngine.Benchmarks.Rendering3D;

/// <summary>
/// One 3D frame (<c>Clear</c>, 13 <c>Draw</c>s, <c>Finish</c>) on the CPU <see cref="Rasterizer"/> and on the
/// <see cref="GlRenderer"/>: a chequered floor and twelve spheres (vertices shared between triangles, like a kart
/// model) with <see cref="Triangles"/> triangles in all. 180 rows is Kartster's 320×180; 720 shows how each renderer
/// scales with pixels. <c>Gpu</c> fails where OpenGL 3.3 is missing; which adapter it ran on is printed at setup.
/// </summary>
[MemoryDiagnoser]
public class RendererBenchmarks
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int SphereCount = 12;
    private static readonly Vector3 ToSun = Vector3.Normalize(new Vector3(0.3f, 1f, 0.2f));

    private IRenderer3D _renderer;
    private Mesh _floor;
    private Mesh _sphere;
    private Matrix4x4[] _worlds;
    private Matrix4x4 _viewProjection;
    private ConsoleColor[] _sky;
    private uint[] _skyRgb;

    //**********************************************************
    //** props
    //**********************************************************

    [Params(180, 720)]
    public int Height { get; set; }

    [Params(false, true)]
    public bool TrueColor { get; set; }

    [Params(20_000, 240_000)]
    public int Triangles { get; set; }

    //**********************************************************
    //** setup
    //**********************************************************

    [GlobalSetup(Target = nameof(Cpu))]
    public void SetupCpu()
    {
        CreateScene();
        _renderer = new Rasterizer(Height * 16 / 9, Height, ConsolePalette.Campbell, TrueColor);
    }

    [GlobalSetup(Target = nameof(Gpu))]
    public void SetupGpu()
    {
        CreateScene();
        // Set BENCHMARK_INTEGRATED_GPU=1 to measure a laptop's integrated GPU instead of its discrete one.
        GlRenderer.PreferDiscreteGpu = Environment.GetEnvironmentVariable("BENCHMARK_INTEGRATED_GPU") != "1";
        var gpu = new GlRenderer(Height * 16 / 9, Height, ConsolePalette.Campbell, TrueColor);
        Console.WriteLine($"// GlRenderer on {gpu.Description}");
        _renderer = gpu;
    }

    [GlobalCleanup]
    public void Cleanup() => _renderer.Dispose();

    //**********************************************************
    //** benchmarks
    //**********************************************************

    [Benchmark(Baseline = true)]
    public void Cpu() => Frame();

    [Benchmark]
    public void Gpu() => Frame();

    //**********************************************************
    //** internals
    //**********************************************************

    private void Frame()
    {
        if (TrueColor)
            _renderer.Clear(_skyRgb);
        else
            _renderer.Clear(_sky);

        _renderer.Draw(_floor, Matrix4x4.Identity, _viewProjection, ToSun);
        for (var i = 0; i < _worlds.Length; i++)
            _renderer.Draw(_sphere, _worlds[i], _viewProjection, ToSun);
        _renderer.Finish();
    }

    private void CreateScene()
    {
        var width = Height * 16 / 9;
        _sky = new ConsoleColor[width * Height];
        _skyRgb = new uint[width * Height];
        Array.Fill(_skyRgb, 0x4080C0u);
        _floor = Mesh.CreateFloor(80, 40, new Vector3(0.2f, 0.6f, 0.3f), new Vector3(0.7f, 0.5f, 0.2f));
        _sphere = CreateSphere((int)MathF.Sqrt((Triangles - _floor.TriangleCount) / (float)SphereCount));
        _worlds = new Matrix4x4[SphereCount];
        for (var i = 0; i < SphereCount; i++) // two rows of six, going away from the camera
            _worlds[i] = Matrix4x4.CreateTranslation(i % 2 == 0 ? -1.5f : 1.5f, 1f, 4f - 3f * (i / 2));
        _viewProjection = Matrix4x4.CreateLookAt(new Vector3(0, 2.2f, 10), new Vector3(0, 1, 0), Vector3.UnitY)
                          * Matrix4x4.CreatePerspectiveFieldOfView(50f * MathF.PI / 180f, 16f / 9f, 0.2f, 400f);
    }

    /// <summary>A unit sphere of about <paramref name="segments"/>² triangles in bands of colour, faces outwards.</summary>
    private static Mesh CreateSphere(int segments)
    {
        var rings = segments / 2;
        var positions = new Vector3[(rings + 1) * segments];
        for (var ring = 0; ring <= rings; ring++)
        for (var segment = 0; segment < segments; segment++)
        {
            var (sinPolar, cosPolar) = MathF.SinCos(MathF.PI * ring / rings);
            var (sinAround, cosAround) = MathF.SinCos(2 * MathF.PI * segment / segments);
            positions[ring * segments + segment] = new Vector3(sinPolar * cosAround, cosPolar, sinPolar * sinAround);
        }

        var indices = new int[rings * segments * 6];
        var colors = new Vector3[rings * segments * 2];
        for (var ring = 0; ring < rings; ring++)
        for (var segment = 0; segment < segments; segment++)
        {
            var quad = ring * segments + segment;
            var next = ring * segments + (segment + 1) % segments;
            indices[quad * 6] = quad;
            indices[quad * 6 + 1] = next;
            indices[quad * 6 + 2] = next + segments;
            indices[quad * 6 + 3] = quad;
            indices[quad * 6 + 4] = next + segments;
            indices[quad * 6 + 5] = quad + segments;
            colors[quad * 2] = colors[quad * 2 + 1] = ring % 2 == 0
                ? new Vector3(0.8f, 0.3f, 0.2f)
                : new Vector3(0.9f, 0.8f, 0.3f);
        }

        return new Mesh(positions, indices, colors);
    }
}
