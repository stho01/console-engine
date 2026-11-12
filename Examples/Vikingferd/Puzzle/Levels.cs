namespace Vikingferd.Puzzle;

/// <summary>
/// The levels, in play order. Map characters:
/// <list type="table">
/// <item><term><c>.</c></term><description>air</description></item>
/// <item><term><c>#</c></term><description>rock</description></item>
/// <item><term><c>%</c></term><description>cracked stone (Gorm smashes it)</description></item>
/// <item><term><c>^</c></term><description>spikes</description></item>
/// <item><term><c>X</c></term><description>exit</description></item>
/// <item><term><c>a</c>-<c>e</c></term><description>door of channel a-e (open while the channel is on)</description></item>
/// <item><term><c>p</c>-<c>t</c></term><description>bridge of channel a-e (solid while the channel is on)</description></item>
/// <item><term><c>A</c>-<c>E</c></term><description>pressure plate of channel a-e</description></item>
/// <item><term><c>1</c>-<c>5</c></term><description>lever of channel a-e</description></item>
/// <item><term><c>Y</c> <c>G</c> <c>S</c></term><description>Ylva, Gorm, Sigrid start here (the tile their feet are in)</description></item>
/// <item><term><c>o</c></term><description>a crate starts here</description></item>
/// </list>
/// A level is at most <see cref="Layout.MaxStageColumns"/> x <see cref="Layout.MaxStageRows"/> tiles and has each
/// hero exactly once (<c>LevelsTests</c> checks both, and plays every level through).
/// </summary>
public static class Levels
{
    public static readonly Level[] All =
    [
        new("The Shore",
            "Ylva jumps high: climb the ledge and pull the lever (E). TAB or 1-3 switches viking.",
            [
                "##############################",
                "#...................#........#",
                "#...................#........#",
                "#...................#........#",
                "#...................#........#",
                "#...................#........#",
                "#...................#........#",
                "#..........1........#........#",
                "#.........#####.....#........#",
                "#...................a........#",
                "#..YGS..............a....XXX.#",
                "##############################"
            ]),
        new("Rockslide",
            "Gorm smashes cracked stone (E) and pushes crates. Build a step up to the exit.",
            [
                "##############################",
                "#.........#..................#",
                "#.........#..................#",
                "#.........#..................#",
                "#.........#..................#",
                "#.........#..................#",
                "#.........#..................#",
                "#.........#..................#",
                "#.........#.............XXX..#",
                "#.........%...........########",
                "#..YGS....%....o......########",
                "##############################"
            ]),
        new("The Gorge",
            "A drop of five tiles knocks a viking out. Sigrid glides: hold jump while she falls.",
            [
                "##############################",
                "#............................#",
                "#............................#",
                "#............................#",
                "#............................#",
                "#.YGS...................XXX..#",
                "#########ppppppppppp##########",
                "#########..........###########",
                "#########.........############",
                "#########........#############",
                "#########^^.A...##############",
                "##############################"
            ]),
        new("Counterweight",
            "A crate can hold a plate down. Stand on each other's heads to reach higher.",
            [
                "##############################",
                "#...........#...........#....#",
                "#...........#...........#....#",
                "#...........#...........#....#",
                "#...........#...........#....#",
                "#...........#......2....#....#",
                "#...........#...######..#....#",
                "#...........#...######..#....#",
                "#...........a...........b....#",
                "#.YGS..o....a...........b.XXX#",
                "#########A####################",
                "##############################"
            ]),
        new("The Long Way Down",
            "Mind the hole: jump over it. Only one viking can go down that way and live.",
            [
                "##############################",
                "#.....#...................#..#",
                "#.....#...................#..#",
                "#.....#...................#..#",
                "#.....%...................b..#",
                "#.YGS.%.o.............C...b..#",
                "###########.ppppppp#########.#",
                "#...................#........#",
                "#...................#........#",
                "#...................#........#",
                "#...................#.......##",
                "#...................#.......##",
                "#...................c.......##",
                "#..............2....c.XXX...##",
                "###########A##################",
                "##############################"
            ])
    ];
}
