using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Native modular frame compositor; no world simulation or texture fitting.</summary>
public sealed class OfficerMotionFrames
{
    public const int Width = 64, Height = 96, FrameCount = 12;
    public readonly string[] Keys;
    public readonly string[] Order;
    public readonly string[] Labels;
    public readonly int IdleDurationMs, WalkDurationMs;
    public readonly string Binding;
    private readonly Dictionary<string, byte[][]> frames = new();
    private readonly string[][] expected;
    public int All => (1 << Keys.Length) - 1;
    public int Head => (1 << Array.IndexOf(Keys, "face")) | (1 << Array.IndexOf(Keys, "hair"));

    public OfficerMotionFrames(string dataPath = "res://Data/officer_motion_trial_v6.json")
    {
        using var data = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(dataPath));
        var root = data.RootElement;
        if (root.GetProperty("canvas")[0].GetInt32() != Width || root.GetProperty("canvas")[1].GetInt32() != Height || root.GetProperty("display_scale").GetInt32() != 1)
            throw new InvalidOperationException("Officer native dimensions changed.");
        Keys = root.GetProperty("keys").EnumerateArray().Select(e => e.GetString()!).ToArray();
        Order = root.GetProperty("order").EnumerateArray().Select(e => e.GetString()!).ToArray();
        Labels = root.GetProperty("labels").EnumerateArray().Select(e => e.GetString()!).ToArray();
        if (Keys.Length != 10 || Keys.Distinct().Count() != 10 || Order.Length != 8 || !Order.ToHashSet().SetEquals(Keys.Skip(2)))
            throw new InvalidOperationException("Officer slots missing or duplicated.");
        IdleDurationMs = root.GetProperty("idle_duration_ms").GetInt32(); WalkDurationMs = root.GetProperty("walk_duration_ms").GetInt32();
        Binding = root.GetProperty("binding").GetString()!;
        foreach (var item in root.GetProperty("sheets").EnumerateObject())
        {
            string path = item.Value.GetProperty("path").GetString()!;
            using var image = GD.Load<Texture2D>(path).GetImage();
            if (image.IsCompressed() && image.Decompress() != Error.Ok) throw new InvalidOperationException("Motion texture decompression failed.");
            if (image.GetWidth() != Width * FrameCount || image.GetHeight() != Height) throw new InvalidOperationException("Motion sheet size: " + path);
            image.Convert(Image.Format.Rgba8);
            var bytes = image.GetData(); var split = new byte[FrameCount][];
            for (int f = 0; f < FrameCount; f++)
            {
                split[f] = new byte[Width * Height * 4];
                for (int y = 0; y < Height; y++) Buffer.BlockCopy(bytes, (y * Width * FrameCount + f * Width) * 4, split[f], y * Width * 4, Width * 4);
                for (int p = 0; p < split[f].Length; p += 4)
                {
                    if (split[f][p + 3] != 0 && split[f][p + 3] != 255) throw new InvalidOperationException("Nonbinary officer alpha.");
                    if (split[f][p + 3] == 0) split[f][p] = split[f][p + 1] = split[f][p + 2] = 0;
                }
                if (Hash(split[f]) != item.Value.GetProperty("frame_hashes")[f].GetString())
                    throw new InvalidOperationException("Imported native frame changed: " + path + " frame=" + f);
            }
            frames[item.Name] = split;
        }
        expected = root.GetProperty("hashes").EnumerateArray().Select(row => row.EnumerateArray().Select(v => v.GetString()!).ToArray()).ToArray();
        if (expected.Length != 1024 || expected.Any(row => row.Length != FrameCount)) throw new InvalidOperationException("Incomplete native regression table.");
    }

    public bool Has(int selection, string key) => (selection & (1 << Array.IndexOf(Keys, key))) != 0;
    public int Without(int selection, params string[] keys)
    {
        foreach (string key in keys) selection &= ~(1 << Array.IndexOf(Keys, key));
        return selection;
    }
    public byte[] Compose(int selection, int frame)
    {
        if (selection < 0 || selection > All || frame < 0 || frame >= FrameCount) throw new ArgumentOutOfRangeException();
        var pixels = (byte[])frames["body"][frame].Clone();
        foreach (string part in Order)
        {
            if (!Has(selection, part)) continue;
            var mask = frames["hide_" + part][frame];
            for (int i = 0; i < pixels.Length; i += 4) if (mask[i + 3] != 0) Array.Clear(pixels, i, 4);
        }
        void Over(string name)
        {
            var layer = frames[name][frame];
            for (int i = 0; i < pixels.Length; i += 4) if (layer[i + 3] != 0) Buffer.BlockCopy(layer, i, pixels, i, 4);
        }
        if (Has(selection, "face")) Over("face");
        foreach (string key in Order) if (Has(selection, key)) Over(key);
        if (!Has(selection, "arms")) Over("hands");
        if (Has(selection, "hair")) Over("hair");
        return pixels;
    }
    public Image MakeImage(int selection, int frame) => Image.CreateFromData(Width, Height, false, Image.Format.Rgba8, Compose(selection, frame));
    public string Expected(int selection, int frame) => expected[selection][frame];
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
