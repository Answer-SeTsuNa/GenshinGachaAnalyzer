namespace GenshinGachaAnalyzer.Core.Helpers;

public static class AppPaths
{
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GenshinGachaAnalyzer");
    public static string DatabasePath => Path.Combine(DataDirectory, "gacha.db");
    public static string SettingsPath => Path.Combine(DataDirectory, "settings.json");
}
