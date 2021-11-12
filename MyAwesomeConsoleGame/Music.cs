using System.Threading;
using System.Threading.Tasks;

namespace MyAwesomeConsoleGame
{
    public static class Music
    {
        public static async Task PlayIntroMusic()
        {
            return;
            for (int i = 37; i <= 32767; i += 2000)
            {
                Thread.Sleep(1);
                System.Console.Beep(i, 200);
            }
        }       
        public static async Task PlayGameOverMusic()
        {
            return;
           System.Console.Beep(2000, 250);
           System.Console.Beep(2500, 250);
           System.Console.Beep(2000, 100);
           System.Console.Beep(2000, 100);
           System.Console.Beep(2000, 100);
        }        
    }
}