using System;
using System.Text;
using Launcher;

Console.OutputEncoding = Encoding.UTF8;

GameLocator locator;
try
{
    locator = GameLocator.FromLauncherOutput(AppContext.BaseDirectory);
}
catch (InvalidOperationException e)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}

new LauncherApp(GameCatalog.Games, locator).Run();
return 0;
