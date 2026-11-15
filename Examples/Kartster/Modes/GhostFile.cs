using System;
using System.IO;

namespace Kartster.Modes;

/// <summary>
/// The best lap of one track and kart on disk, binary, little-endian: magic <c>KGHO</c>, format version, the fixed
/// step in seconds, the lap time in seconds, the pose count, then per pose X, Z, height and radians (floats). Written
/// through a temp file, as EternalDigger's <c>SaveFile</c>, so a crash never leaves half a file. A file that doesn't
/// read back exactly (damaged, another version or step, out-of-range numbers) is ignored: no ghost, and the next best
/// lap overwrites it.
/// </summary>
public static class GhostFile
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int Version = 1;

    /// <summary>"KGHO" as a little-endian int.</summary>
    private const int Magic = 0x4F48474B;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// <c>Kartster/ghosts/&lt;track&gt;.&lt;kart&gt;.ghost</c> in the user's local app-data folder
    /// (<c>%LOCALAPPDATA%</c> on Windows).
    /// </summary>
    public static string PathFor(string track, string kart) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kartster", "ghosts",
        $"{track}.{kart}.ghost");

    /// <summary>Writes <paramref name="lap"/> to <paramref name="path"/>. Never throws.</summary>
    /// <returns>False when the file couldn't be written.</returns>
    public static bool Save(string path, GhostLap lap, float stepSeconds)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var temp = path + ".tmp";
            using (var stream = File.Create(temp))
                Write(stream, lap, stepSeconds);

            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Reads the file at <paramref name="path"/> into <paramref name="lap"/>. Never throws.</summary>
    /// <returns>False (and <paramref name="lap"/> empty) when there is no file or it is not a good one.</returns>
    public static bool Load(string path, GhostLap lap, float stepSeconds)
    {
        try
        {
            if (!File.Exists(path))
            {
                lap.Clear();
                return false;
            }

            using var stream = File.OpenRead(path);
            return Read(stream, lap, stepSeconds);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            lap.Clear();
            return false;
        }
    }

    public static void Write(Stream stream, GhostLap lap, float stepSeconds)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(stepSeconds);
        writer.Write(lap.LapSeconds);
        writer.Write(lap.Count);
        foreach (var pose in lap.Poses)
        {
            writer.Write(pose.X);
            writer.Write(pose.Z);
            writer.Write(pose.Height);
            writer.Write(pose.Radians);
        }
    }

    /// <returns>False (and <paramref name="lap"/> empty) on anything but a whole, valid ghost.</returns>
    public static bool Read(Stream stream, GhostLap lap, float stepSeconds)
    {
        lap.Clear();
        try
        {
            using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            if (reader.ReadInt32() != Magic || reader.ReadInt32() != Version || reader.ReadSingle() != stepSeconds)
                return false;

            var seconds = reader.ReadSingle();
            var count = reader.ReadInt32();
            if (!(seconds > 0f && float.IsFinite(seconds)) || count < 1 || count > lap.Capacity)
                return false;

            for (var i = 0; i < count; i++)
            {
                var pose = new GhostPose(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                    reader.ReadSingle());
                if (!float.IsFinite(pose.X) || !float.IsFinite(pose.Z) || !float.IsFinite(pose.Height) ||
                    !float.IsFinite(pose.Radians))
                {
                    lap.Clear();
                    return false;
                }

                lap.Add(pose);
            }

            if (stream.CanSeek && stream.Position != stream.Length)
            {
                lap.Clear();
                return false;
            }

            lap.LapSeconds = seconds;
            return true;
        }
        catch (EndOfStreamException)
        {
            lap.Clear();
            return false;
        }
    }
}
