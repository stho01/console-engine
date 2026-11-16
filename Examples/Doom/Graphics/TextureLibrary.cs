using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Doom.Wad;

namespace Doom.Graphics;

/// <summary>
/// Wall textures (composed from <c>PNAMES</c> patches by <c>TEXTURE1</c>/<c>TEXTURE2</c>) and flats (the lumps
/// between <c>F_START</c> and <c>F_END</c>), looked up by name. Animated textures and flats cycle through their frames
/// with <see cref="Animate"/>: renderers look textures and flats up through <see cref="TextureFrame"/> and
/// <see cref="FlatFrame"/>.
/// </summary>
public sealed class TextureLibrary
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string SkyFlatName = "F_SKY1";
    public const int NoTexture = -1;

    /// <summary>Tics each frame of an animated texture or flat shows.</summary>
    public const int AnimationTics = 8;

    private const int TextureHeaderSize = 22;
    private const int PatchEntrySize = 10;

    /// <summary>First and last frame of each animated flat sequence (frames are consecutive lumps).</summary>
    private static readonly (string First, string Last)[] FlatAnimations =
    [
        ("NUKAGE1", "NUKAGE3"), ("FWATER1", "FWATER4"), ("SWATER1", "SWATER4"), ("LAVA1", "LAVA4"),
        ("BLOOD1", "BLOOD3"), ("RROCK05", "RROCK08"), ("SLIME01", "SLIME04"), ("SLIME05", "SLIME08"),
        ("SLIME09", "SLIME12")
    ];

    /// <summary>First and last frame of each animated wall texture sequence (frames are consecutive textures).</summary>
    private static readonly (string First, string Last)[] TextureAnimations =
    [
        ("BLODGR1", "BLODGR4"), ("SLADRIP1", "SLADRIP3"), ("BLODRIP1", "BLODRIP4"), ("FIREWALA", "FIREWALL"),
        ("GSTFONT1", "GSTFONT3"), ("FIRELAVA", "FIRELAV2"), ("FIREMAG1", "FIREMAG3"), ("FIREBLU1", "FIREBLU2"),
        ("ROCKRED1", "ROCKRED3"), ("BFALL1", "BFALL4"), ("SFALL1", "SFALL4"), ("WFALL1", "WFALL4"),
        ("DBRAIN1", "DBRAIN4")
    ];

    private readonly List<Texture> _textures = [];
    private readonly List<Flat> _flats = [];
    private readonly Dictionary<string, int> _textureByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _flatByName = new(StringComparer.Ordinal);
    private readonly List<(int First, int Count)> _textureCycles = [];
    private readonly List<(int First, int Count)> _flatCycles = [];
    private int[] _textureFrame = [];
    private int[] _flatFrame = [];

    //**********************************************************
    //** props
    //**********************************************************

    public IReadOnlyList<Texture> Textures => _textures;
    public IReadOnlyList<Flat> Flats => _flats;

    /// <summary>The sky flat's index, or <see cref="NoTexture"/>. Ceilings (or floors) with it show the sky.</summary>
    public int SkyFlat { get; private set; } = NoTexture;

    /// <summary>The texture each texture index shows this tic (itself unless it is animated).</summary>
    public ReadOnlySpan<int> TextureFrame => _textureFrame;

    /// <summary>The flat each flat index shows this tic (itself unless it is animated).</summary>
    public ReadOnlySpan<int> FlatFrame => _flatFrame;

    //**********************************************************
    //** public methods
    //**********************************************************

    public static TextureLibrary Load(WadFile wad)
    {
        var library = new TextureLibrary();
        library.LoadTextures(wad);
        library.LoadFlats(wad);
        library.FinishLoading();
        return library;
    }

    /// <summary>Adds a texture (tests, or a texture built in code). Call <see cref="FinishLoading"/> after adding.</summary>
    public int AddTexture(Texture texture)
    {
        _textures.Add(texture);
        _textureByName.TryAdd(texture.Name, _textures.Count - 1);
        return _textures.Count - 1;
    }

    /// <summary>Adds a flat (tests, or a flat built in code). Call <see cref="FinishLoading"/> after adding.</summary>
    public int AddFlat(Flat flat)
    {
        _flats.Add(flat);
        _flatByName[flat.Name] = _flats.Count - 1;
        return _flats.Count - 1;
    }

    /// <summary>Resolves the sky flat and the animation sequences, and resets every animation to its first frame.</summary>
    public void FinishLoading()
    {
        SkyFlat = FindFlat(SkyFlatName);
        _textureFrame = new int[_textures.Count];
        _flatFrame = new int[_flats.Count];
        _textureCycles.Clear();
        _flatCycles.Clear();
        foreach (var (first, last) in TextureAnimations)
            AddCycle(_textureCycles, FindTexture(first), FindTexture(last));
        foreach (var (first, last) in FlatAnimations)
            AddCycle(_flatCycles, FindFlat(first), FindFlat(last));

        Animate(0);
    }

    /// <summary>The texture called <paramref name="name"/>, or <see cref="NoTexture"/> (also for <c>-</c>).</summary>
    public int FindTexture(string name) =>
        !string.IsNullOrEmpty(name) && name != "-" && _textureByName.TryGetValue(name, out var index) ? index : NoTexture;

    /// <summary>The flat called <paramref name="name"/>, or <see cref="NoTexture"/>.</summary>
    public int FindFlat(string name) =>
        !string.IsNullOrEmpty(name) && _flatByName.TryGetValue(name, out var index) ? index : NoTexture;

    /// <summary>Points every animated texture and flat at its frame for game tic <paramref name="tic"/>.</summary>
    public void Animate(int tic)
    {
        for (var i = 0; i < _textureFrame.Length; i++)
            _textureFrame[i] = i;
        for (var i = 0; i < _flatFrame.Length; i++)
            _flatFrame[i] = i;

        var step = tic / AnimationTics;
        foreach (var (first, count) in _textureCycles)
        {
            for (var i = 0; i < count; i++)
                _textureFrame[first + i] = first + (i + step) % count;
        }

        foreach (var (first, count) in _flatCycles)
        {
            for (var i = 0; i < count; i++)
                _flatFrame[first + i] = first + (i + step) % count;
        }
    }

    /// <summary>
    /// The other face of a switch texture: <c>SW1…</c> ↔ <c>SW2…</c>, or <see cref="NoTexture"/> when
    /// <paramref name="texture"/> is not a switch.
    /// </summary>
    public int SwitchPartner(int texture)
    {
        if (texture < 0 || texture >= _textures.Count)
            return NoTexture;

        var name = _textures[texture].Name;
        if (name.StartsWith("SW1", StringComparison.Ordinal))
            return FindTexture("SW2" + name[3..]);
        if (name.StartsWith("SW2", StringComparison.Ordinal))
            return FindTexture("SW1" + name[3..]);

        return NoTexture;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static void AddCycle(List<(int First, int Count)> cycles, int first, int last)
    {
        if (first >= 0 && last > first)
            cycles.Add((first, last - first + 1));
    }

    private void LoadTextures(WadFile wad)
    {
        if (!wad.Contains("PNAMES"))
            return;

        var patchNames = wad.Read("PNAMES");
        var patchCount = BinaryPrimitives.ReadInt32LittleEndian(patchNames);
        var patches = new Picture[patchCount];
        var patchLoaded = new bool[patchCount];
        var patchLumps = new int[patchCount];
        for (var i = 0; i < patchCount; i++)
            patchLumps[i] = wad.IndexOf(WadFile.ReadName(patchNames.Slice(4 + i * 8, 8)));

        foreach (var lumpName in new[] { "TEXTURE1", "TEXTURE2" })
        {
            if (!wad.Contains(lumpName))
                continue;

            var lump = wad.Read(lumpName);
            var count = BinaryPrimitives.ReadInt32LittleEndian(lump);
            for (var t = 0; t < count; t++)
            {
                var offset = BinaryPrimitives.ReadInt32LittleEndian(lump[(4 + t * 4)..]);
                var header = lump[offset..];
                var name = WadFile.ReadName(header[..8]);
                var width = BinaryPrimitives.ReadInt16LittleEndian(header[12..]);
                var height = BinaryPrimitives.ReadInt16LittleEndian(header[14..]);
                var patchesInTexture = BinaryPrimitives.ReadInt16LittleEndian(header[20..]);
                if (width <= 0 || height <= 0)
                    continue;

                var texture = new Texture(name, width, height);
                for (var p = 0; p < patchesInTexture; p++)
                {
                    var entry = header[(TextureHeaderSize + p * PatchEntrySize)..];
                    var originX = BinaryPrimitives.ReadInt16LittleEndian(entry);
                    var originY = BinaryPrimitives.ReadInt16LittleEndian(entry[2..]);
                    var patchIndex = BinaryPrimitives.ReadInt16LittleEndian(entry[4..]);
                    if (patchIndex < 0 || patchIndex >= patchCount || patchLumps[patchIndex] < 0)
                        continue;

                    if (!patchLoaded[patchIndex])
                    {
                        patchLoaded[patchIndex] = true;
                        var patchLump = patchLumps[patchIndex];
                        patches[patchIndex] = Picture.Decode(wad.Read(patchLump), wad.Lumps[patchLump].Name);
                    }

                    Blit(texture, patches[patchIndex], originX, originY);
                }

                AddTexture(texture);
            }
        }
    }

    private static void Blit(Texture texture, Picture patch, int originX, int originY)
    {
        for (var px = 0; px < patch.Width; px++)
        {
            var tx = originX + px;
            if (tx < 0 || tx >= texture.Width)
                continue;

            for (var py = 0; py < patch.Height; py++)
            {
                var ty = originY + py;
                if (ty < 0 || ty >= texture.Height || !patch.Opaque[px * patch.Height + py])
                    continue;

                texture.Pixels[tx * texture.Height + ty] = patch.Pixels[px * patch.Height + py];
                texture.Opaque[tx * texture.Height + ty] = true;
            }
        }
    }

    private void LoadFlats(WadFile wad)
    {
        var start = wad.IndexOf("F_START");
        if (start < 0)
            start = wad.IndexOf("FF_START");
        var end = wad.IndexOf("F_END");
        if (end < 0)
            end = wad.IndexOf("FF_END");
        if (start < 0 || end <= start)
            return;

        for (var i = start + 1; i < end; i++)
        {
            var lump = wad.Read(i);
            if (lump.Length < Flat.Size * Flat.Size)
                continue;

            AddFlat(new Flat(wad.Lumps[i].Name, lump[..(Flat.Size * Flat.Size)].ToArray()));
        }
    }
}
