using System;
using System.IO;
using System.Linq;
using Kartster;

// Optional arguments: a folder of kart folders (e.g. Kartster's Content/3D/Rendered/Karts), then a folder of
// exported tracks (<id>.ribbon.obj + <id>.spline.obj); without them the game reads Assets/Karts and Assets/Tracks
// next to the exe. --gpu starts on the OpenGL renderer (G toggles in the game),
// --truecolor in 24-bit colour (C toggles).
var gpu = args.Contains("--gpu", StringComparer.OrdinalIgnoreCase);
var trueColor = args.Contains("--truecolor", StringComparer.OrdinalIgnoreCase);
var folders = args.Where(arg => !arg.StartsWith("--", StringComparison.Ordinal)).ToArray();
var game = new KartsterGame(
    folders.Length > 0 ? folders[0] : null,
    folders.Length > 1 ? folders[1] : null,
    gpu,
    trueColor)
{
    Name = "Kartster"
};

game.Initialize();
game.Start();
