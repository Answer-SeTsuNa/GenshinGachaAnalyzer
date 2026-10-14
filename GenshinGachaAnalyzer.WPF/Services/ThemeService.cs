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
    private sealed class NativeAppearance
    {
        public int Dark = -1, CaptionColor = -1, TextColor = -1;
        public bool? Acrylic;
        public bool Moving;
        public int Backdrop = -1;
    }
    public static string StyleKey { get; private set; } = "Gear";
    public static UserSettings Appearance { get; private set; } = new();
    public static bool SupportsAcrylic => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);
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
        var background = ((SolidColorBrush)Application.Current.Resources["Background"]).Color;
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            // 玻璃背景可能透出深色桌面，不能只切换浅色按钮；标题栏固定使用主题底色。
            int caption = ColorRef(background);
            int text = ColorRef(((SolidColorBrush)Application.Current.Resources["Text"]).Color);
            if (state.CaptionColor != caption && DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int)) == 0)
                state.CaptionColor = caption;
            if (state.TextColor != text && DwmSetWindowAttribute(handle, 36, ref text, sizeof(int)) == 0)
                state.TextColor = text;
        }
        if (window is not Views.MainWindow) return;
        bool requested = Appearance.Acrylic && SupportsAcrylic;
        if (state.Acrylic == requested)
        {
            // 实色回退也应随日夜/角色更新，避免合成目标保留上一款主题的颜色。
            if (!requested && HwndSource.FromHwnd(handle)?.CompositionTarget is { } opaqueTarget)
                opaqueTarget.BackgroundColor = background;
            return;
        }
        // 参考 FufuLauncher 的 DesktopAcrylicBackdrop + 独立遮罩结构。
        // WPF 使用同类 DWM Desktop Acrylic，不混用旧式 AccentPolicy，也不在拖动时改实色。
        int backdrop = requested ? 3 : 1;
        bool acrylic = false;
        if (SupportsAcrylic && DwmSetWindowAttribute(handle, 38, ref backdrop, sizeof(int)) == 0)
        { state.Backdrop = backdrop; acrylic = requested; }
        state.Acrylic = requested;
        window.SetResourceReference(Window.BackgroundProperty, acrylic ? "WindowTint" : "Background");
        int inset = acrylic ? -1 : 0;
        var margin = new Margins { Left = inset, Right = inset, Top = inset, Bottom = inset };
        DwmExtendFrameIntoClientArea(handle, ref margin);
        if (HwndSource.FromHwnd(handle)?.CompositionTarget is { } target)
            target.BackgroundColor = acrylic ? Colors.Transparent : background;
    }
    internal static void SetInteractiveMove(Window window, bool moving)
    {
        var state = NativeStates.GetOrCreateValue(window);
        if (state.Moving == moving) return;
        // 移动不再改变材质或背景颜色，遮罩滑块在启动、静止和拖动时使用同一层。
        state.Moving = moving;
    }
    internal static bool IsInteractiveMove(Window window) => NativeStates.GetOrCreateValue(window).Moving;
    internal static (int Background, int Text) AppliedCaptionColors(Window window)
    {
        var state = NativeStates.GetOrCreateValue(window);
        return (state.CaptionColor, state.TextColor); // 仅缓存 DwmSetWindowAttribute 成功的值。
    }
    private static int ColorRef(Color color) => color.R | color.G << 8 | color.B << 16;
    public static void Stop()
    {
        if (_listening) SystemEvents.UserPreferenceChanged -= SystemChanged;
        _listening = false;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Margins { public int Left, Right, Top, Bottom; }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
}
