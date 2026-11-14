namespace Kartster.Ui;

/// <summary>
/// A scene that draws UI over the 3D picture. The game calls <see cref="DrawUi"/> after <c>Renderer.Finish</c> (a
/// scene's own <c>OnRender</c> runs before that, while the 3D frame is still being drawn).
/// </summary>
public interface IUiLayer
{
    void DrawUi(IPixelTarget target);
}
