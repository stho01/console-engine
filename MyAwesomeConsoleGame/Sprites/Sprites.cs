using ConsoleEngine.Infrastructure.Rendering;
using MyAwesomeConsoleGame.Entities.Tiles;

namespace MyAwesomeConsoleGame.Sprites
{
    public static class Sprites
    {
        public static readonly Sprite Sprite = Sprite.FromStringArray(new[]
        {
            "┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼",
            "███▀▀▀██┼███▀▀▀███┼███▀█▄█▀███┼██▀▀▀",
            "██┼┼┼┼██┼██┼┼┼┼┼██┼██┼┼┼█┼┼┼██┼██┼┼┼",
            "██┼┼┼▄▄▄┼██▄▄▄▄▄██┼██┼┼┼▀┼┼┼██┼██▀▀▀",
            "██┼┼┼┼██┼██┼┼┼┼┼██┼██┼┼┼┼┼┼┼██┼██┼┼┼",
            "███▄▄▄██┼██┼┼┼┼┼██┼██┼┼┼┼┼┼┼██┼██▄▄▄",
            "┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼",
            "███▀▀▀███┼▀███┼┼██▀┼██▀▀▀┼██▀▀▀▀██▄┼",
            "██┼┼┼┼┼██┼┼┼██┼┼██┼┼██┼┼┼┼██┼┼┼┼┼██┼",
            "██┼┼┼┼┼██┼┼┼██┼┼██┼┼██▀▀▀┼██▄▄▄▄▄▀▀┼",
            "██┼┼┼┼┼██┼┼┼██┼┼█▀┼┼██┼┼┼┼██┼┼┼┼┼██┼",
            "███▄▄▄███┼┼┼─▀█▀┼┼─┼██▄▄▄┼██┼┼┼┼┼██▄",
            "┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼┼",
            "                                    ",
            "    ---  PRESS R TO RESTART  ---    ",
            "                                    "
        });


        public static Sprite Story(string playername)
        {
            return Sprite.FromStringArray(new[]
            {
                @"          _______                  ______         __  __         ",
                @"         |__   __|                |  ____|       |  \/  |        ",
                @"            | | ___ _ __ _ __ __ _| |__ ___  _ __| \  / |        ",
                @"            | |/ _ \ '__| '__/ _` |  __/ _ \| '__| |\/| |        ",
                @"            | |  __/ |  | | | (_| | | | (_) | |  | |  | |        ",
                @"            |_|\___|_|  |_|  \__,_|_|  \___/|_|  |_|  |_|        ",
                @"                                                                 ",
                @"                                                                 ",
                @"                                                                 ",
                @" _______________________________________________________________ ",
                @"|                                                               |",
                @"| HUMANS HAVE SPENT 15.000 YEARS CREATING AN ATMOSPHERE ON MARS |",
                @"| BUT TO NO AVAIL                                               |",
                @"|                                                               |",
                InsertPlayerNameSentence(playername),
                @"| ROVER PILOT AT GASA FOR 37 YEARS,                             |",
                @"| HAVE TAKEN MATTERS INTO YOUR OWN HANDS                        |",
                @"| YOU REMOTE CONTROL THE MARS ROVER juniper creek FROM EARTH    |",
                @"| PLANTING ATHOMSPHERIC GENERATOR COILS ALL AROUND MARS         |",
                @"|                                                               |",
                @"|                                                               |",
                @"| BUT BE WARE - YOU MUST GET TO THE NEXT POWER PLANT            |",
                @"| BEFORE YOU RUN OUT OF JUICE                                   |",
                @"|                                                               |",
                @"|  press H to continue                                          |",
                @"|_______________________________________________________________|"
            });
        }

        private static string InsertPlayerNameSentence(string playername)
        {
           // INSERT PLAYER NAME FILL OUT TO 64 CHARS INC | 
           var str = $@"| IT SEEMS IMPOSSIBLE; BUT YOU,{playername}";
           str = str.PadRight(64, ' ');
           str += "|";
           return str;
        }
    }
}