using System.IO;
using KartViewer;

// Optional argument: a folder of kart folders, e.g. Kartster's Content/3D/Rendered/Karts.
var game = new KartViewerGame(args.Length > 0 ? args[0] : Path.Combine("Assets", "Karts"))
{
    Name = "KartViewer"
};

game.Initialize();
game.Start();
