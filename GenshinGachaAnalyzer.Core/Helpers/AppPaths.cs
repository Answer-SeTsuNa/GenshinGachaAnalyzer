using System.Runtime.InteropServices;

namespace GenshinGachaAnalyzer.Core.Helpers;

public static class AppPaths
{
    public static bool HasPackageIdentity => OperatingSystem.IsWindows() && DetectPackageIdentity();
    private static bool DetectPackageIdentity()
    {
        int length = 0;
        return GetCurrentPackageFullName(ref length, IntPtr.Zero) == 122; // ERROR_INSUFFICIENT_BUFFER: 当前进程有包身份。
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, IntPtr packageFullName);
    public static string ResolveDataDirectory(bool packaged, string roamingDirectory, string userProfile) =>
        Path.Combine(packaged ? userProfile : roamingDirectory, packaged ? "木偶祈愿档案" : "GenshinGachaAnalyzer");
    // 商店包的 AppData 可能随卸载清理。档案使用用户拥有的独立目录，保留导出和整库备份入口。
    public static string DataDirectory => ResolveDataDirectory(HasPackageIdentity,
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    public static string DatabasePath => Path.Combine(DataDirectory, "gacha.db");
    public static string SettingsPath => Path.Combine(DataDirectory, "settings.json");
}
