using Doom.Play;

namespace Doom.Rendering;

/// <summary>
/// Draws the 3D view of a level into a <see cref="FrameBuffer"/> as palette indices. Two implementations, switched
/// with V while playing (<see cref="ViewMode"/>): <see cref="WorldRenderer"/> (Doom's own BSP column renderer) and
/// <see cref="Meshes.MeshRenderer"/> (the engine's <c>ConsoleEngine.Rendering3D</c> triangle rasterizer).
/// </summary>
public interface IViewRenderer
{
    FrameBuffer Frame { get; }

    /// <summary>
    /// Renders <paramref name="level"/> from <paramref name="camera"/>. <paramref name="fixedLight"/> is a light
    /// table to use everywhere (light amplification, invulnerability), or -1.
    /// </summary>
    void Render(Level level, in Camera camera, int skyTexture, int fixedLight = -1);
}

/// <summary>Which <see cref="IViewRenderer"/> draws the view.</summary>
public enum ViewMode
{
    /// <summary><see cref="WorldRenderer"/>: textured walls, floors and sprites, light by sector and distance.</summary>
    Bsp,

    /// <summary><see cref="Meshes.MeshRenderer"/>: the level as triangles on <c>ConsoleEngine.Rendering3D</c>.</summary>
    Mesh
}
