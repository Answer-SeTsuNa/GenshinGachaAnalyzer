using System.Text.Json;
using GenshinGachaAnalyzer.Core.Helpers;

namespace GenshinGachaAnalyzer.Core.Services;

public sealed record UserSettings(string? GamePath = null, string Theme = "System", double UiScale = 0, string StyleKey = "Gear",
    bool Acrylic = true, bool SemiTransparent = true, double OverlayOpacity = .72, double PanelOpacity = .88,
    double WallpaperOpacity = .65, double WallpaperBlur = 18, string? WallpaperPath = null, bool DeveloperMode = false, bool HideUid = false);
public sealed class SettingsService(string path)
{
    public UserSettings Load()
    {
        if (!File.Exists(path)) return new();
        try { return Normalize(JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path)) ?? new()); }
        catch (JsonException) { throw new UserFacingException("设置文件无法读取。抽卡数据库不受影响，可在设置页重新保存设置。"); }
    }
    public void Save(UserSettings settings)
    {
        settings = Normalize(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static UserSettings Normalize(UserSettings settings)
    {
        double Bounded(double value, double min, double max, double fallback) => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
        return settings with { StyleKey = settings.StyleKey == "Puppet" ? "Gear" : settings.StyleKey,
            OverlayOpacity = Bounded(settings.OverlayOpacity, .1, 1, .72), PanelOpacity = Bounded(settings.PanelOpacity, .35, 1, .88),
            WallpaperOpacity = Bounded(settings.WallpaperOpacity, 0, 1, .65), WallpaperBlur = Bounded(settings.WallpaperBlur, 0, 40, 18) };
    }
}
