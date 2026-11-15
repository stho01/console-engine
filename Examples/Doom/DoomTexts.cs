namespace Doom;

/// <summary>Every user-visible string of the Doom example, including exception messages.</summary>
public static class DoomTexts
{
    //**********************************************************
    //** title
    //**********************************************************

    public const string Title = "D O O M   ( c o n s o l e )";
    public const string TitleHint = "↑ ↓ pick a map   ENTER start   ESC quit";
    public const string TitleWad = "WAD: {0}";
    public const string TitleControls1 = "W S / ↑ ↓ move   A D strafe   ← → turn";
    public const string TitleControls2 = "SPACE fire   E use   1-7 weapon   TAB map   F11 gamma";

    //**********************************************************
    //** missing wad
    //**********************************************************

    public const string MissingWadTitle = "DOOM1.WAD NOT FOUND";
    public const string MissingWadLine1 = "This example plays the levels of the shareware Doom WAD,";
    public const string MissingWadLine2 = "which is not part of this repository. Get DOOM1.WAD (shareware v1.9)";
    public const string MissingWadLine3 = "and put it next to Doom.exe, or set the DOOM_WAD environment variable";
    public const string MissingWadLine4 = "to its full path. Places searched:";
    public const string MissingWadQuit = "ESC quit";
    public const string BadWadTitle = "COULD NOT LOAD THE WAD";

    //**********************************************************
    //** hud
    //**********************************************************

    public const string HudHealth = "HEALTH";
    public const string HudArmor = "ARMOR";
    public const string HudAmmo = "AMMO";
    public const string HudKeys = "KEYS";
    public const string HudBullets = "BULL";
    public const string HudShells = "SHEL";
    public const string HudRockets = "RCKT";
    public const string HudCells = "CELL";
    public const string HudKills = "K {0}/{1}  I {2}/{3}  S {4}/{5}";
    public const string HudDead = "YOU DIED - press SPACE or E to restart the level";

    //**********************************************************
    //** messages
    //**********************************************************

    public const string NeedBlueKey = "You need a blue key to open this.";
    public const string NeedYellowKey = "You need a yellow key to open this.";
    public const string NeedRedKey = "You need a red key to open this.";
    public const string SecretFound = "SECRET FOUND";
    public const string GotStimpack = "+10 health (stimpack)";
    public const string GotMedikit = "+25 health (medikit)";
    public const string GotHealthBonus = "+1 health";
    public const string GotSoulSphere = "+100 health (soul sphere)";
    public const string GotArmorBonus = "+1 armor";
    public const string GotGreenArmor = "Armor (100)";
    public const string GotBlueArmor = "Mega armor (200)";
    public const string GotBullets = "Bullets";
    public const string GotShells = "Shells";
    public const string GotRockets = "Rockets";
    public const string GotCells = "Cells";
    public const string GotBackpack = "Backpack: more ammo room";
    public const string GotBlueKey = "Blue key";
    public const string GotYellowKey = "Yellow key";
    public const string GotRedKey = "Red key";
    public const string GotChainsaw = "Chainsaw!";
    public const string GotShotgun = "Shotgun!";
    public const string GotChaingun = "Chaingun!";
    public const string GotRocketLauncher = "Rocket launcher!";
    public const string GotPlasmaGun = "Plasma gun!";
    public const string GotBfg = "BFG!";
    public const string GotBerserk = "Berserk!";
    public const string GotInvulnerability = "Invulnerability!";
    public const string GotRadiationSuit = "Radiation suit";
    public const string GotInvisibility = "Partial invisibility";
    public const string GotComputerMap = "Computer map";
    public const string GotLightAmp = "Light amplification goggles";

    //**********************************************************
    //** weapons
    //**********************************************************

    public const string WeaponFist = "FIST";
    public const string WeaponChainsaw = "CHAINSAW";
    public const string WeaponPistol = "PISTOL";
    public const string WeaponShotgun = "SHOTGUN";
    public const string WeaponChaingun = "CHAINGUN";
    public const string WeaponRocketLauncher = "ROCKET L.";
    public const string WeaponPlasmaGun = "PLASMA";
    public const string WeaponBfg = "BFG";

    //**********************************************************
    //** intermission
    //**********************************************************

    public const string Finished = "{0} FINISHED";
    public const string Kills = "KILLS   {0}%";
    public const string Items = "ITEMS   {0}%";
    public const string Secrets = "SECRETS {0}%";
    public const string Time = "TIME    {0}:{1:00}";
    public const string NextMap = "ENTER  on to {0}";
    public const string EpisodeDone = "EPISODE COMPLETE - thanks for playing!";
    public const string BackToTitle = "ENTER  back to the title";

    //**********************************************************
    //** errors
    //**********************************************************

    public const string WadTooShort = "Not a WAD file: shorter than its 12 byte header.";
    public const string WadBadId = "Not a WAD file: it starts with \"{0}\" instead of IWAD or PWAD.";
    public const string WadBadDirectory = "Broken WAD file: the lump directory lies outside the file.";
    public const string WadBadLump = "Broken WAD file: lump {0} lies outside the file.";
    public const string LumpMissing = "The WAD has no lump named {0}.";
    public const string LumpTooShort = "Lump {0} is too short.";
    public const string MapMissing = "The WAD has no map {0}.";
    public const string MapLumpMissing = "Map {0} has no {1} lump.";
    public const string PictureBroken = "Broken picture lump: {0}.";
}
