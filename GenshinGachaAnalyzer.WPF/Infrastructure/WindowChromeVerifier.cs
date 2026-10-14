using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GenshinGachaAnalyzer.WPF.Services;
using GenshinGachaAnalyzer.WPF.Views;

namespace GenshinGachaAnalyzer.WPF.Infrastructure;

/// <summary>隔离回归覆盖原生标题栏颜色和实际 HWND 移动消息，避免只验证客户区截图。</summary>
internal static class WindowChromeVerifier
{
    public static void VerifyCaption(MainWindow window, string output)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        var handle = new WindowInteropHelper(window).Handle;
        var background = ((SolidColorBrush)Application.Current.Resources["Background"]).Color;
        var text = ((SolidColorBrush)Application.Current.Resources["Text"]).Color;
        // 微软仅将这两项定义为可设置属性，当前系统 Get 返回 E_INVALIDARG。
        // 核对系统已接受的设置值；实际按钮可见性另以原生窗口截图检查。
        var applied = ThemeService.AppliedCaptionColors(window);
        if (applied.Background != ColorRef(background) || applied.Text != ColorRef(text))
            throw new InvalidOperationException("系统未接受标题栏的主题背景 / 文字色。");
        double a = Luminance(background), b = Luminance(text);
        double contrast = (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
        if (contrast < 4.5) throw new InvalidOperationException("标题栏文字与背景对比度不足。");
        File.AppendAllText(Path.Combine(output, "caption-verification.txt"),
            $"{ThemeService.StyleKey}/{ThemeService.Preference}：原生设置成功；色值对比度 {contrast:F2}:1（不替代按钮视觉检查）。\n");
    }

    public static async Task VerifyMoveAsync(MainWindow window, System.Windows.Controls.Image wallpaper, string output)
    {
        var settings = ThemeService.Appearance;
        var handle = new WindowInteropHelper(window).Handle;
        var source = wallpaper.Source;
        var timings = new List<object>();
        try
        {
            ThemeService.Configure(settings with { Acrylic = true });
            byte expectedAlpha = ThemeService.SupportsAcrylic ? (byte)0 : (byte)255;
            var backgroundBrush = window.Background;
            for (int round = 0; round < 3; round++)
            {
                var watch = Stopwatch.StartNew();
                SendMessage(handle, 0x0231, IntPtr.Zero, IntPtr.Zero);
                double enterMs = watch.Elapsed.TotalMilliseconds;
                if (!ThemeService.IsInteractiveMove(window) || !ReferenceEquals(window.Background, backgroundBrush)
                    || HwndSource.FromHwnd(handle)?.CompositionTarget?.BackgroundColor.A != expectedAlpha)
                    throw new InvalidOperationException("进入移动循环时改变了背景材质 / 透明度。");
                if (!ThemeService.Appearance.Acrylic) throw new InvalidOperationException("拖动改变了用户的模糊设置。");
                // 模拟移动期间更换配色及重复进入通知，仍保持相同透明度。
                ThemeService.Apply(round % 2 == 0 ? "Light" : "Dark");
                SendMessage(handle, 0x0231, IntPtr.Zero, IntPtr.Zero);
                if (window.Background is not SolidColorBrush movingBrush || movingBrush.Color.A != expectedAlpha)
                    throw new InvalidOperationException("移动期间主题刷新改变了背景透明度。");
                await window.Dispatcher.InvokeAsync(window.UpdateLayout);
                watch.Restart();
                SendMessage(handle, 0x0232, IntPtr.Zero, IntPtr.Zero);
                double exitMs = watch.Elapsed.TotalMilliseconds;
                if (ThemeService.IsInteractiveMove(window) || window.Background is not SolidColorBrush endBrush || endBrush.Color.A != expectedAlpha)
                    throw new InvalidOperationException("结束 / 取消移动后背景透明度发生跳变。");
                backgroundBrush = window.Background;
                if (!ReferenceEquals(source, wallpaper.Source)) throw new InvalidOperationException("拖动重新生成了壁纸。");
                timings.Add(new { Round = round, EnterMs = enterMs, ExitMs = exitMs });
            }
            ThemeService.Configure(settings with { Acrylic = false });
            SendMessage(handle, 0x0231, IntPtr.Zero, IntPtr.Zero);
            SendMessage(handle, 0x0232, IntPtr.Zero, IntPtr.Zero);
            if (window.Background is not SolidColorBrush { Color.A: 255 } || ThemeService.Appearance.Acrylic)
                throw new InvalidOperationException("关闭模糊时拖动错误地开启了模糊。");
            File.WriteAllText(Path.Combine(output, "window-move-verification.json"), JsonSerializer.Serialize(timings, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { ThemeService.SetInteractiveMove(window, false); ThemeService.Configure(settings); }
    }
    private static int ColorRef(Color color) => color.R | color.G << 8 | color.B << 16;
    private static double Luminance(Color color)
    {
        static double Channel(byte value) { double s = value / 255d; return s <= .04045 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4); }
        return .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
    }
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
}
