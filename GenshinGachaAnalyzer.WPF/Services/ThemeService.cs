using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using GenshinGachaAnalyzer.Core.Services;

namespace GenshinGachaAnalyzer.WPF.Services;

/// <summary>跟随系统默认应用主题；原生 DWM 材质由系统合成，不使用低性能的透明分层窗口。</summary>
public static class ThemeService
{
    public static string Preference { get; private set; } = "System";
    public static bool IsDark { get; private set; }
    public static event Action? Changed;
    private static bool _listening;
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, NativeAppearance> NativeStates = new();
    private sealed class NativeAppearance { public int Dark = -1; public int Backdrop = -1; }
    public static string StyleKey { get; private set; } = "Gear";
    public static UserSettings Appearance { get; private set; } = new();
    public static void Configure(UserSettings settings) { Appearance = settings; Apply(settings.Theme, settings.StyleKey); }
    public static void Apply(string preference, string? styleKey = null)
    {
        string previousStyle = StyleKey;
        bool previousDark = IsDark;
        StyleKey = styleKey ?? StyleKey;
        var profile = ThemeProfile.All.FirstOrDefault(p => p.Key == StyleKey) ?? ThemeProfile.All[0];
        StyleKey = profile.Key;
        Preference = preference is "Light" or "Dark" ? preference : "System";
        bool dark = Preference == "Dark" || Preference == "System" && SystemIsDark();
        IsDark = dark;
        var colors = new Dictionary<string, string>
        {
            ["Background"] = dark ? "#191D2B" : "#F4F2FA",
            ["WindowTint"] = dark ? "#B0191D2B" : "#B0F4F2FA",
            ["Panel"] = dark ? "#EE252C40" : "#F2FFFFFF",
            ["PanelRaised"] = dark ? "#2E364D" : "#F1EFF8",
            ["Line"] = dark ? "#444E68" : "#DDD7EB",
            ["Text"] = dark ? "#F1F0FC" : "#293047",
            ["Muted"] = dark ? "#B7BED5" : "#68718A",
            ["Gold"] = dark ? "#BEADF6" : "#7662AD",
            ["Error"] = dark ? "#FF9C9C" : "#B84D4D",
            ["Selected"] = dark ? "#413B61" : "#EAE3FC",
            ["SelectedText"] = dark ? "#F0E8FF" : "#604888",
            ["PrimaryText"] = dark ? "#17242A" : "#FFFFFF",
            ["QualityGold"] = dark ? "#E1BC7C" : "#A77636",
            ["QualityPurple"] = dark ? "#C3A9ED" : "#8064A8"
        };
        colors["Background"] = dark ? profile.DarkBackground : profile.LightBackground;
        colors["WindowTint"] = "#00" + colors["Background"][1..];
        colors["Gold"] = dark ? profile.NightAccent : profile.Accent;
        colors["SelectedText"] = dark ? profile.NightAccent : profile.Accent;
        var accent = (Color)ColorConverter.ConvertFromString(dark ? profile.NightAccent : profile.Accent);
        var backdrop = (Color)ColorConverter.ConvertFromString(dark ? profile.DarkBackground : profile.LightBackground);
        colors["Selected"] = $"#{(byte)(backdrop.R * .82 + accent.R * .18):X2}{(byte)(backdrop.G * .82 + accent.G * .18):X2}{(byte)(backdrop.B * .82 + accent.B * .18):X2}";
        colors["PanelRaised"] = $"#{(byte)(backdrop.R * .94 + accent.R * .06):X2}{(byte)(backdrop.G * .94 + accent.G * .06):X2}{(byte)(backdrop.B * .94 + accent.B * .06):X2}";
        double opacity = Appearance.SemiTransparent ? Appearance.PanelOpacity : 1;
        colors["Panel"] = $"#{(byte)Math.Round(opacity * 255):X2}" + (dark ? colors["PanelRaised"][1..] : "FFFFFF");
        colors["Line"] = $"#{(byte)(backdrop.R * .72 + accent.R * .28):X2}{(byte)(backdrop.G * .72 + accent.G * .28):X2}{(byte)(backdrop.B * .72 + accent.B * .28):X2}";
        foreach (var (key, value) in colors)
        {
            var color = (Color)ColorConverter.ConvertFromString(value);
            // 透明度滑块只更新变化的画刷，避免所有缓存页面的动态资源一并失效。
            if (Application.Current.Resources[key] is SolidColorBrush current && current.Color == color) continue;
            var brush = new SolidColorBrush(color); brush.Freeze();
            Application.Current.Resources[key] = brush;
        }
        if (previousStyle != StyleKey || previousDark != IsDark || Application.Current.Resources["ThemeDrawing"] is not Drawing)
        {
            Application.Current.Resources["ThemeDrawing"] = ThemeArtwork.Create(profile.Key, accent);
            Application.Current.Resources["ThemeWatermark"] = ThemeArtwork.CreateWatermark(profile.Key, accent);
        }
        // 简约元素方案统一细描边，不再叠加复杂渐变高光与多重外框。
        if (!ReferenceEquals(Application.Current.Resources["CardEdge"], Application.Current.Resources["Line"]))
            Application.Current.Resources["CardEdge"] = Application.Current.Resources["Line"];
        var corner = new CornerRadius(profile.Key is "Klee" or "Varesa" ? 16 : 12);
        if (!Equals(Application.Current.Resources["ThemeCorner"], corner)) Application.Current.Resources["ThemeCorner"] = corner;
        if (!_listening) { SystemEvents.UserPreferenceChanged += SystemChanged; _listening = true; }
        foreach (Window window in Application.Current.Windows) ApplyWindow(window);
        Changed?.Invoke();
    }
    private static bool SystemIsDark()
    {
        try { return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int value && value == 0; }
        catch (Exception error) when (error is System.Security.SecurityException or IOException or UnauthorizedAccessException) { return false; }
    }
    private static void SystemChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (Preference == "System") Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            // WM_SETTINGCHANGE 还包含输入法、叠加层和其他系统设置通知。
            // 仅实际日夜变化时重绘，避免通知触发重复资源/DWM 更新。
            if (Preference == "System" && SystemIsDark() != IsDark) Apply("System");
        });
    }
    public static void ApplyWindow(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle; if (handle == IntPtr.Zero) return;
        var state = NativeStates.GetOrCreateValue(window);
        int dark = IsDark ? 1 : 0;
        if (state.Dark != dark && DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int)) == 0) state.Dark = dark;
        if (window is not Views.MainWindow || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621)) return;
        int backdrop = Appearance.Acrylic ? 3 : 1; // DWMSBT_TRANSIENTWINDOW：Desktop Acrylic；关闭时用实色回退。
        window.SetResourceReference(Window.BackgroundProperty, Appearance.Acrylic ? "WindowTint" : "Background");
        if (state.Backdrop == backdrop) return; // 换角色配色不重新配置系统材质。
        if (DwmSetWindowAttribute(handle, 38, ref backdrop, sizeof(int)) != 0) return;
        state.Backdrop = backdrop;
        int inset = Appearance.Acrylic ? -1 : 0;
        var margin = new Margins { Left = inset, Right = inset, Top = inset, Bottom = inset };
        DwmExtendFrameIntoClientArea(handle, ref margin);
        if (HwndSource.FromHwnd(handle)?.CompositionTarget is { } target)
            target.BackgroundColor = Appearance.Acrylic ? Colors.Transparent : ((SolidColorBrush)Application.Current.Resources["Background"]).Color;
    }
    public static void Stop()
    {
        if (_listening) SystemEvents.UserPreferenceChanged -= SystemChanged;
        _listening = false;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Margins { public int Left, Right, Top, Bottom; }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
}
