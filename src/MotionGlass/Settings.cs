using System.Text.Json;

namespace MotionGlass;

internal enum BlurMode { Velocity, FrameBlending, Hybrid, AccumulationMax, AccumulationMix }

internal sealed record Settings(BlurMode Mode = BlurMode.Velocity, int Amount = 50, bool RefreshScaling = false)
{
    public bool FixedStrength => Mode is BlurMode.FrameBlending or BlurMode.Hybrid;
    public bool HasEffect => FixedStrength || Amount > 0;
    public static readonly string[] Names = ["Velocity based", "Frame blending", "Hybrid blending", "Accumulation max", "Accumulation mix"];
    public string Explanation => Mode switch
    {
        BlurMode.Velocity => "Directional blur follows motion estimated between screen frames.",
        BlurMode.FrameBlending => "Blends the four most recent frames into a soft, layered trail. Strength is fixed.",
        BlurMode.Hybrid => "Combines directional motion blur and frame blending. Strength is fixed.",
        BlurMode.AccumulationMax => "Keeps the brightest pixels from recent frames, with fading light trails.",
        _ => "Mixes each new frame with a fading history for smooth, lingering trails."
    };
    private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MotionGlass");
    public static Settings Load()
    {
        try
        {
            var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(Folder, "settings.json")));
            return s is null || !Enum.IsDefined(s.Mode) ? new() : s with { Amount = Math.Clamp(s.Amount, 0, 100) };
        }
        catch { return new(); }
    }
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, "settings.json");
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this));
            File.Move(path + ".tmp", path, true);
        }
        catch { /* Settings persistence must never prevent switching the effect off. */ }
    }
}
