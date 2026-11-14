using System.IO;
using KartViewer;

// Optional arguments: a folder of kart folders (e.g. Kartster's Content/3D/Rendered/Karts), then a folder of
// exported tracks (<id>.ribbon.obj + <id>.spline.obj).
var game = new KartViewerGame(
    args.Length > 0 ? args[0] : Path.Combine("Assets", "Karts"),
    args.Length > 1 ? args[1] : Path.Combine("Assets", "Tracks"))
{
    Name = "KartViewer"
};

game.Initialize();
game.Start();
