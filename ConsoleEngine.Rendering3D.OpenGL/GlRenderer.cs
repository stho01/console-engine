using System;
using System.Collections.Generic;
using System.Numerics;
using Silk.NET.Core.Contexts;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace ConsoleEngine.Rendering3D.OpenGL;

/// <summary>
/// Draws meshes on the GPU (OpenGL 3.3 core, hidden GLFW window) into an off-screen palette-index image and reads it
/// back in <see cref="Finish"/>. The picture is the one <see cref="Rasterizer"/> draws: same flat light, the same
/// per-pixel local lights (<see cref="Lighting"/>, in the fragment shader), dither,
/// palette lookup, screen-door transparency, culling and near-plane clipping; only edge pixels can differ.
/// In <see cref="TrueColor"/> mode the image is RGBA8 instead, with its own fragment shader (its output type differs
/// from the palette index's), the background copied in by <c>Clear</c> and see-through triangles alpha-blended in a
/// second pass per mesh with depth writes off, like the CPU.
/// The GL context belongs to the thread that created the renderer: use it only from that thread (the game loop).
/// </summary>
public sealed class GlRenderer : IRenderer3D
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const byte NoGeometry = 255;
    private const int FloatsPerVertex = 10; // position, normal, colour, opacity

    private const string VertexShader = """
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec3 aNormal;
        layout(location = 2) in vec3 aColor;
        layout(location = 3) in float aOpacity;
        uniform mat4 uWorldViewProjection;
        uniform mat4 uWorld;
        uniform vec3 uToLight;
        uniform vec3 uAmbient;
        uniform vec3 uSunColor;
        uniform bool uUnlit;
        flat out vec3 vColor;
        flat out vec3 vAlbedo;
        flat out vec3 vNormal;
        flat out float vOpacity;
        out vec3 vWorld;
        void main() {
            gl_Position = uWorldViewProjection * vec4(aPosition, 1.0);
            gl_Position.z = 2.0 * gl_Position.z - gl_Position.w; // System.Numerics z 0..1 -> GL -w..w
            vec3 normal = normalize(mat3(uWorld) * aNormal);
            vColor = uUnlit ? aColor : aColor * (uAmbient + uSunColor * max(0.0, dot(normal, uToLight)));
            vAlbedo = aColor;
            vNormal = normal;
            vWorld = (uWorld * vec4(aPosition, 1.0)).xyz;
            vOpacity = aOpacity;
        }
        """;

    /// <summary>
    /// The flat colour plus the local lights at this pixel, as <c>SceneLighting.Contribution</c> computes them;
    /// shared by both fragment shaders. <c>uLightCount</c> is 0 for unlit meshes.
    /// </summary>
    private const string LightsShader = """
        flat in vec3 vColor;
        flat in vec3 vAlbedo;
        flat in vec3 vNormal;
        flat in float vOpacity;
        in vec3 vWorld;
        uniform int uLightCount;
        uniform vec3 uLightPosition[16];
        uniform vec3 uLightColor[16];
        uniform vec3 uLightDirection[16];
        uniform float uLightRadius[16];
        uniform float uLightCone[16];
        vec3 shade() {
            if (uLightCount == 0)
                return vColor;
            vec3 sum = vec3(0.0);
            for (int i = 0; i < uLightCount; i++) {
                vec3 toLight = uLightPosition[i] - vWorld;
                float squared = dot(toLight, toLight);
                float fraction = clamp(1.0 - squared / (uLightRadius[i] * uLightRadius[i]), 0.0, 1.0);
                if (fraction <= 0.0)
                    continue;
                vec3 direction = toLight / max(sqrt(squared), 0.001);
                float facing = clamp(dot(vNormal, direction), 0.0, 1.0);
                float cone = uLightCone[i] < -0.5
                    ? 1.0
                    : smoothstep(uLightCone[i], uLightCone[i] + 0.08, -dot(direction, uLightDirection[i]));
                sum += uLightColor[i] * (fraction * fraction * facing * cone);
            }
            return vColor + vAlbedo * sum;
        }
        """;

    private const string FragmentShader = """
        uniform usampler3D uNearest;
        uniform float uDither[16];
        uniform int uHeight;
        out uint paletteIndex;
        const float Bayer[16] = float[16](0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5);
        void main() {
            int x = int(gl_FragCoord.x);
            int y = uHeight - 1 - int(gl_FragCoord.y); // the CPU counts rows from the top
            int cell = ((y & 3) << 2) | (x & 3);
            if (vOpacity < 1.0 && (Bayer[cell] + 0.5) / 16.0 >= vOpacity)
                discard;
            ivec3 rgb = ivec3(clamp(shade() + uDither[cell], 0.0, 1.0) * 31.0 + 0.5);
            paletteIndex = texelFetch(uNearest, ivec3(rgb.b, rgb.g, rgb.r), 0).r;
        }
        """;

    private const string TrueColorFragmentShader = """
        uniform bool uBlendedPass;
        out vec4 color;
        void main() {
            if ((vOpacity < 1.0) != uBlendedPass)
                discard; // opaque triangles in the first pass, see-through ones in the second
            color = vec4(clamp(shade(), 0.0, 1.0), vOpacity);
        }
        """;

    private readonly IWindow _window;
    private readonly GL _gl;
    private readonly uint _framebuffer;
    private readonly uint _colorBuffer;
    private readonly uint _depthBuffer;
    private readonly uint _backgroundFramebuffer; // true colour: Clear uploads the background here and blits it over
    private readonly uint _backgroundTexture;
    private readonly uint _nearestTexture;
    private readonly uint _program;
    private readonly int _worldViewProjectionLocation;
    private readonly int _worldLocation;
    private readonly int _toLightLocation;
    private readonly int _unlitLocation;
    private readonly int _blendedPassLocation;
    private readonly int _ambientLocation;
    private readonly int _sunColorLocation;
    private readonly int _lightCountLocation;
    private readonly int _lightPositionLocation;
    private readonly int _lightColorLocation;
    private readonly int _lightDirectionLocation;
    private readonly int _lightRadiusLocation;
    private readonly int _lightConeLocation;
    private readonly float[] _lightPositions = new float[SceneLighting.MaxLights * 3];
    private readonly float[] _lightColors = new float[SceneLighting.MaxLights * 3];
    private readonly float[] _lightDirections = new float[SceneLighting.MaxLights * 3];
    private readonly float[] _lightRadii = new float[SceneLighting.MaxLights];
    private readonly float[] _lightCones = new float[SceneLighting.MaxLights];

    // shortcut: a mesh stays on the GPU until Dispose; add eviction if games create meshes per level or per frame.
    private readonly Dictionary<Mesh, GpuMesh> _meshes = new();
    private readonly ConsoleColor[] _background;
    private readonly byte[] _pixels;
    private readonly uint[] _bgra; // true colour: the read-back, 0xAARRGGBB, rows bottom-up
    private readonly uint[] _clearColor = [NoGeometry, 0, 0, 0];
    private readonly float[] _clearDepth = [1f];
    private bool _disposed;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <exception cref="InvalidOperationException">No OpenGL 3.3 context or framebuffer: fall back to
    /// <see cref="Rasterizer"/>.</exception>
    /// <param name="palette">The console colours of palette mode; not used when <paramref name="trueColor"/>.</param>
    /// <param name="trueColor">Draw 24-bit colour into <see cref="Rgb"/> instead of palette indices.</param>
    public GlRenderer(int width, int height, ConsolePalette palette, bool trueColor = false)
    {
        Width = width;
        Height = height;
        TrueColor = trueColor;
        Colors = trueColor ? [] : new ConsoleColor[width * height];
        Rgb = trueColor ? new uint[width * height] : [];
        _background = trueColor ? [] : new ConsoleColor[width * height];
        _pixels = trueColor ? [] : new byte[width * height];
        _bgra = trueColor ? new uint[width * height] : [];

        try
        {
            _window = Window.Create(WindowOptions.Default with
            {
                IsVisible = false,
                Size = new(16, 16),
                VSync = false,
                API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible,
                    new APIVersion(3, 3))
            });
            _window.Initialize();
            _gl = GL.GetApi(_window);

            if (trueColor)
                (_backgroundFramebuffer, _backgroundTexture) = CreateBackground();
            (_framebuffer, _colorBuffer, _depthBuffer) = CreateFramebuffer();
            _program = CreateProgram(trueColor ? TrueColorFragmentShader : FragmentShader);
            _worldViewProjectionLocation = _gl.GetUniformLocation(_program, "uWorldViewProjection");
            _worldLocation = _gl.GetUniformLocation(_program, "uWorld");
            _toLightLocation = _gl.GetUniformLocation(_program, "uToLight");
            _unlitLocation = _gl.GetUniformLocation(_program, "uUnlit");
            _ambientLocation = _gl.GetUniformLocation(_program, "uAmbient");
            _sunColorLocation = _gl.GetUniformLocation(_program, "uSunColor");
            _lightCountLocation = _gl.GetUniformLocation(_program, "uLightCount");
            _lightPositionLocation = _gl.GetUniformLocation(_program, "uLightPosition");
            _lightColorLocation = _gl.GetUniformLocation(_program, "uLightColor");
            _lightDirectionLocation = _gl.GetUniformLocation(_program, "uLightDirection");
            _lightRadiusLocation = _gl.GetUniformLocation(_program, "uLightRadius");
            _lightConeLocation = _gl.GetUniformLocation(_program, "uLightCone");
            if (trueColor)
            {
                _blendedPassLocation = _gl.GetUniformLocation(_program, "uBlendedPass");
                _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            }
            else
            {
                _nearestTexture = CreateNearestTexture(palette);
                _gl.Uniform1(_gl.GetUniformLocation(_program, "uNearest"), 0);
                _gl.Uniform1(_gl.GetUniformLocation(_program, "uHeight"), height);
                _gl.Uniform1(_gl.GetUniformLocation(_program, "uDither"), ConsolePalette.Dither);
            }

            _gl.Viewport(0, 0, (uint)width, (uint)height);
            _gl.Enable(EnableCap.DepthTest);
            _gl.DepthFunc(DepthFunction.Less);
            _gl.Enable(EnableCap.CullFace);
            _gl.CullFace(TriangleFace.Back);
            _gl.FrontFace(FrontFaceDirection.Ccw);
            _gl.PixelStore(PixelStoreParameter.PackAlignment, 1);

            var error = _gl.GetError();
            if (error != GLEnum.NoError)
                throw new InvalidOperationException($"setting up failed with {error}");
        }
        catch (Exception exception)
        {
            Dispose();
            throw new InvalidOperationException($"OpenGL 3.3 is not available: {exception.Message}", exception);
        }
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int Width { get; }
    public int Height { get; }
    public bool TrueColor { get; }

    /// <summary>The palette-mode frame, <c>y * Width + x</c>, after <see cref="Finish"/>. Empty in true colour.</summary>
    public ConsoleColor[] Colors { get; }

    /// <summary>The true-colour frame, <c>0xRRGGBB</c>, after <see cref="Finish"/>. Empty in palette mode.</summary>
    public uint[] Rgb { get; }

    /// <inheritdoc/>
    public SceneLighting Lighting { get; set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Like the constructor, but returns false instead of throwing when OpenGL 3.3 is not available.</summary>
    public static bool TryCreate(int width, int height, ConsolePalette palette, out GlRenderer renderer) =>
        TryCreate(width, height, palette, false, out renderer);

    /// <summary>Like the constructor, but returns false instead of throwing when OpenGL 3.3 is not available.</summary>
    public static bool TryCreate(int width, int height, ConsolePalette palette, bool trueColor,
        out GlRenderer renderer)
    {
        try
        {
            renderer = new GlRenderer(width, height, palette, trueColor);
            return true;
        }
        catch (Exception)
        {
            // Window.Create can also fail without a GLFW library or windowing platform, not only on OpenGL.
            renderer = null;
            return false;
        }
    }

    /// <summary>Starts a frame: remembers the background (shown where nothing is drawn) and clears the GPU image.</summary>
    public void Clear(ReadOnlySpan<ConsoleColor> background)
    {
        if (TrueColor)
            throw new InvalidOperationException(Rasterizer.PaletteClearInTrueColor);

        MakeCurrent();
        background.CopyTo(_background);
        Lighting = null;
        _gl.ClearBuffer(BufferKind.Color, 0, _clearColor.AsSpan()); // glClear is undefined on an integer target
        _gl.ClearBuffer(BufferKind.Depth, 0, _clearDepth.AsSpan());
    }

    /// <summary>
    /// Starts a true-colour frame: uploads the background and copies it into the GPU image (flipped to OpenGL's
    /// bottom-up rows), so blending sees the real background.
    /// </summary>
    public void Clear(ReadOnlySpan<uint> backgroundRgb)
    {
        if (!TrueColor)
            throw new InvalidOperationException(Rasterizer.TrueColorClearInPaletteMode);

        MakeCurrent();
        _gl.BindTexture(TextureTarget.Texture2D, _backgroundTexture);
        _gl.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, (uint)Width, (uint)Height, PixelFormat.Bgra,
            PixelType.UnsignedInt8888Rev, backgroundRgb[..(Width * Height)]);
        _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _backgroundFramebuffer);
        _gl.BlitFramebuffer(0, 0, Width, Height, 0, Height, Width, 0, ClearBufferMask.ColorBufferBit,
            BlitFramebufferFilter.Nearest);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
        _gl.ClearBuffer(BufferKind.Depth, 0, _clearDepth.AsSpan());
        Lighting = null;
    }

    /// <param name="toLight">Unit vector from the scene towards the sun, in world space.</param>
    public unsafe void Draw(Mesh mesh, Matrix4x4 world, Matrix4x4 viewProjection, Vector3 toLight)
    {
        var gpuMesh = Upload(mesh);
        var worldViewProjection = world * viewProjection;

        // System.Numerics is row-major with row vectors; read as column-major it is the transpose GLSL needs.
        _gl.UniformMatrix4(_worldViewProjectionLocation, 1, false, &worldViewProjection.M11);
        _gl.UniformMatrix4(_worldLocation, 1, false, &world.M11);
        _gl.Uniform3(_toLightLocation, toLight.X, toLight.Y, toLight.Z);
        _gl.Uniform1(_unlitLocation, mesh.IsUnlit ? 1 : 0);
        UploadLighting(mesh.IsUnlit);
        _gl.BindVertexArray(gpuMesh.VertexArray);
        var vertexCount = (uint)(mesh.TriangleCount * 3);
        if (!TrueColor)
        {
            _gl.DrawArrays(PrimitiveType.Triangles, 0, vertexCount);
            return;
        }

        _gl.Uniform1(_blendedPassLocation, 0);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, vertexCount);
        if (mesh.TriangleOpacity == null)
            return;

        // See-through triangles: blended over what is drawn, depth-tested but not written.
        _gl.Uniform1(_blendedPassLocation, 1);
        _gl.DepthMask(false);
        _gl.Enable(EnableCap.Blend);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, vertexCount);
        _gl.Disable(EnableCap.Blend);
        _gl.DepthMask(true); // ClearBuffer(Depth) needs it
    }

    /// <summary>Reads the frame back into <see cref="Colors"/> (waits for the GPU).</summary>
    public void Finish()
    {
        if (TrueColor)
        {
            FinishTrueColor();
            return;
        }

        _gl.ReadPixels(0, 0, (uint)Width, (uint)Height, PixelFormat.RedInteger, PixelType.UnsignedByte,
            _pixels.AsSpan());

        for (var y = 0; y < Height; y++)
        {
            var row = (Height - 1 - y) * Width; // OpenGL rows go bottom-up
            var index = y * Width;
            for (var x = 0; x < Width; x++, index++)
            {
                var value = _pixels[row + x];
                Colors[index] = value == NoGeometry ? _background[index] : (ConsoleColor)value;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_gl != null)
        {
            _window.GLContext!.MakeCurrent();
            foreach (var gpuMesh in _meshes.Values)
            {
                _gl.DeleteBuffer(gpuMesh.VertexBuffer);
                _gl.DeleteVertexArray(gpuMesh.VertexArray);
            }

            _meshes.Clear();
            _gl.DeleteProgram(_program);
            _gl.DeleteTexture(_nearestTexture);
            _gl.DeleteFramebuffer(_backgroundFramebuffer);
            _gl.DeleteTexture(_backgroundTexture);
            _gl.DeleteFramebuffer(_framebuffer);
            _gl.DeleteRenderbuffer(_colorBuffer);
            _gl.DeleteRenderbuffer(_depthBuffer);
            _gl.Dispose();
        }

        _window?.Dispose();
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void MakeCurrent()
    {
        if (!_window.GLContext!.IsCurrent)
            _window.GLContext.MakeCurrent(); // another renderer on this thread made its context current
    }

    /// <summary>The ambient, the sun's colour and the local lights (none for an unlit mesh) of the next draw.</summary>
    private unsafe void UploadLighting(bool unlit)
    {
        var ambient = Lighting?.Ambient ?? SceneLighting.DefaultAmbient;
        var sunColor = Lighting?.SunColor ?? SceneLighting.DefaultSunColor;
        _gl.Uniform3(_ambientLocation, ambient.X, ambient.Y, ambient.Z);
        _gl.Uniform3(_sunColorLocation, sunColor.X, sunColor.Y, sunColor.Z);

        var lights = unlit || Lighting == null ? default : Lighting.Lights;
        _gl.Uniform1(_lightCountLocation, lights.Length);
        if (lights.Length == 0)
            return;

        for (var i = 0; i < lights.Length; i++)
        {
            ref readonly var light = ref lights[i];
            Store(_lightPositions, i, light.Position);
            Store(_lightColors, i, light.Color);
            Store(_lightDirections, i, light.Direction);
            _lightRadii[i] = light.Radius;
            _lightCones[i] = light.ConeCosine;
        }

        var count = (uint)lights.Length;
        fixed (float* positions = _lightPositions, colors = _lightColors, directions = _lightDirections)
        fixed (float* radii = _lightRadii, cones = _lightCones)
        {
            _gl.Uniform3(_lightPositionLocation, count, positions);
            _gl.Uniform3(_lightColorLocation, count, colors);
            _gl.Uniform3(_lightDirectionLocation, count, directions);
            _gl.Uniform1(_lightRadiusLocation, count, radii);
            _gl.Uniform1(_lightConeLocation, count, cones);
        }

        static void Store(float[] values, int index, Vector3 value)
        {
            values[index * 3] = value.X;
            values[index * 3 + 1] = value.Y;
            values[index * 3 + 2] = value.Z;
        }
    }

    private void FinishTrueColor()
    {
        _gl.ReadPixels(0, 0, (uint)Width, (uint)Height, PixelFormat.Bgra, PixelType.UnsignedInt8888Rev,
            _bgra.AsSpan());

        for (var y = 0; y < Height; y++)
        {
            var row = (Height - 1 - y) * Width; // OpenGL rows go bottom-up
            var index = y * Width;
            for (var x = 0; x < Width; x++, index++)
                Rgb[index] = _bgra[row + x] & 0xFFFFFF;
        }
    }

    private (uint Framebuffer, uint Color, uint Depth) CreateFramebuffer()
    {
        var format = TrueColor ? InternalFormat.Rgba8 : InternalFormat.R8ui;
        var framebuffer = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
        var color = CreateRenderbuffer(format, FramebufferAttachment.ColorAttachment0);
        var depth = CreateRenderbuffer(InternalFormat.DepthComponent24, FramebufferAttachment.DepthAttachment);

        var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            throw new InvalidOperationException(
                $"the {(TrueColor ? "RGBA8" : "R8UI")} framebuffer is not complete ({status})");
        return (framebuffer, color, depth);
    }

    /// <summary>An RGBA8 texture in its own framebuffer, the source <c>Clear</c> blits the background from.</summary>
    private unsafe (uint Framebuffer, uint Texture) CreateBackground()
    {
        var texture = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, texture);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)Width, (uint)Height, 0,
            PixelFormat.Bgra, PixelType.UnsignedInt8888Rev, null);

        var framebuffer = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D, texture, 0);
        var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            throw new InvalidOperationException($"the background framebuffer is not complete ({status})");
        return (framebuffer, texture);
    }

    private uint CreateRenderbuffer(InternalFormat format, FramebufferAttachment attachment)
    {
        var renderbuffer = _gl.GenRenderbuffer();
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, renderbuffer);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, format, (uint)Width, (uint)Height);
        _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, attachment, RenderbufferTarget.Renderbuffer,
            renderbuffer);
        return renderbuffer;
    }

    /// <summary>The palette's 32³ nearest-colour table as a 3D texture: x = blue, y = green, z = red.</summary>
    private uint CreateNearestTexture(ConsolePalette palette)
    {
        var nearest = palette.Nearest;
        var texels = new byte[nearest.Length];
        for (var i = 0; i < texels.Length; i++)
            texels[i] = (byte)nearest[i];

        var texture = _gl.GenTexture();
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture3D, texture);
        _gl.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
        _gl.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        const uint size = ConsolePalette.LookupSize;
        _gl.TexImage3D<byte>(TextureTarget.Texture3D, 0, InternalFormat.R8ui, size, size, size, 0,
            PixelFormat.RedInteger, PixelType.UnsignedByte, texels);
        return texture;
    }

    private uint CreateProgram(string fragmentShader)
    {
        var program = _gl.CreateProgram();
        var vertex = CompileShader(ShaderType.VertexShader, VertexShader);
        var fragment = CompileShader(ShaderType.FragmentShader, "#version 330 core\n" + LightsShader + fragmentShader);
        _gl.AttachShader(program, vertex);
        _gl.AttachShader(program, fragment);
        _gl.LinkProgram(program);
        _gl.DeleteShader(vertex);
        _gl.DeleteShader(fragment);

        _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var linked);
        if (linked == 0)
            throw new InvalidOperationException($"the shaders did not link: {_gl.GetProgramInfoLog(program)}");
        _gl.UseProgram(program);
        return program;
    }

    private uint CompileShader(ShaderType type, string source)
    {
        var shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out var compiled);
        if (compiled == 0)
            throw new InvalidOperationException($"the {type} did not compile: {_gl.GetShaderInfoLog(shader)}");
        return shader;
    }

    /// <summary>The mesh's GPU copy, uploaded the first time and again whenever <see cref="Mesh.Version"/> moved.</summary>
    private unsafe GpuMesh Upload(Mesh mesh)
    {
        if (_meshes.TryGetValue(mesh, out var gpuMesh))
        {
            if (gpuMesh.Version == mesh.Version)
                return gpuMesh;
        }
        else
        {
            gpuMesh = new GpuMesh(new float[mesh.TriangleCount * 3 * FloatsPerVertex], _gl.GenVertexArray(),
                _gl.GenBuffer());
            _gl.BindVertexArray(gpuMesh.VertexArray);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, gpuMesh.VertexBuffer);
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(gpuMesh.Vertices.Length * sizeof(float)), null,
                BufferUsageARB.DynamicDraw);
            const uint stride = FloatsPerVertex * sizeof(float);
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
            _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
            _gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, stride, (void*)(6 * sizeof(float)));
            _gl.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, stride, (void*)(9 * sizeof(float)));
            for (uint attribute = 0; attribute < 4; attribute++)
                _gl.EnableVertexAttribArray(attribute);
            _meshes.Add(mesh, gpuMesh);
        }

        Expand(mesh, gpuMesh.Vertices);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, gpuMesh.VertexBuffer);
        _gl.BufferSubData<float>(BufferTargetARB.ArrayBuffer, 0, gpuMesh.Vertices);
        gpuMesh.Version = mesh.Version;
        return gpuMesh;
    }

    /// <summary>Three vertices per triangle, each carrying the triangle's normal, colour and opacity.</summary>
    private static void Expand(Mesh mesh, float[] vertices)
    {
        var v = 0;
        for (var t = 0; t < mesh.TriangleCount; t++)
        {
            var normal = mesh.TriangleNormals[t];
            var color = mesh.TriangleColors[t];
            var opacity = mesh.TriangleOpacity == null ? 1f : mesh.TriangleOpacity[t];
            for (var corner = 0; corner < 3; corner++)
            {
                var position = mesh.Positions[mesh.Indices[t * 3 + corner]];
                vertices[v++] = position.X;
                vertices[v++] = position.Y;
                vertices[v++] = position.Z;
                vertices[v++] = normal.X;
                vertices[v++] = normal.Y;
                vertices[v++] = normal.Z;
                vertices[v++] = color.X;
                vertices[v++] = color.Y;
                vertices[v++] = color.Z;
                vertices[v++] = opacity;
            }
        }
    }

    private sealed class GpuMesh(float[] vertices, uint vertexArray, uint vertexBuffer)
    {
        public float[] Vertices { get; } = vertices;
        public uint VertexArray { get; } = vertexArray;
        public uint VertexBuffer { get; } = vertexBuffer;
        public int Version { get; set; } = -1;
    }
}
