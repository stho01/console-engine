using Microsoft.Xna.Framework;

namespace Platformer.Maps
{
    public class Level1 : IMap
    {
        public string[] Tiles => MapTiles;

        public Rectangle[] BoundingBoxes => new[] {
            // Dette kan sansynligvis gjøres automagisk 
            new Rectangle(0,  0, 1, 60), // left edge
            new Rectangle(59, 0, 1, 60), // right edge
            new Rectangle(0, 59, 60, 1), // ground 
            // new Rectangle(12, 35, 24, 1), // 1. platau from top 
            // new Rectangle(2, 24,  29, 1), // 2. platau 
            // new Rectangle(30, 38, 30, 1), // 3. platau 
            // new Rectangle(2, 48,  29, 1), // 4. platau 
            // new Rectangle(38, 55,  21, 1), // 5. platau 
        };

        private static readonly string[] MapTiles = new[]
        {
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                 ######################## #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "# #############################                            #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                            ############################# #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "# #########################                                #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "#                                    ##################### #",
            "#                                                          #",
            "#                                                          #",
            "#                                                          #",
            "############################################################",
        };

    }
}