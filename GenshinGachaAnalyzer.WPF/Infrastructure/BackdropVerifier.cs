using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GenshinGachaAnalyzer.WPF.Controls;
using GenshinGachaAnalyzer.WPF.Services;
using GenshinGachaAnalyzer.WPF.Views;

namespace GenshinGachaAnalyzer.WPF.Infrastructure;

/// <summary>隔离检查：原生材质保持不变，遮罩按设定 Alpha 绘制。无需屏幕捕获或前台操作。</summary>
internal static class BackdropVerifier
{
    public static async Task RunAsync(MainWindow window, string output)
    {
        var settings = ThemeService.Appearance;
        var handle = new WindowInteropHelper(window).Handle;
        var background = ((System.Windows.Controls.Grid)window.Content).Children.OfType<ThemeBackdrop>().Single();
        var results = new List<string>();
        try
        {
            foreach (double opacity in new[] { .1, .45, .72, .95 })
            {
                ThemeService.Configure(settings with { Acrylic = true, SemiTransparent = true, OverlayOpacity = opacity, WallpaperPath = null });
                await background.Ready; window.UpdateLayout();
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);
                byte before = MaskAlpha(background);
                SendMessage(handle, 0x0231, IntPtr.Zero, IntPtr.Zero);
                byte moving = MaskAlpha(background);
                SendMessage(handle, 0x0232, IntPtr.Zero, IntPtr.Zero);
                byte after = MaskAlpha(background);
                int expected = (int)Math.Round(opacity * 255);
                if (Math.Abs(before - expected) > 2 || moving != before || after != before)
                    throw new InvalidOperationException("遮罩 Alpha 与设定不一致，或移动时发生变化。");
                if (ThemeService.SupportsAcrylic && (DwmGetWindowAttribute(handle, 38, out int mode, sizeof(int)) != 0 || mode != 3))
                    throw new InvalidOperationException("原生 Desktop Acrylic 未保持启用。");
                results.Add($"遮罩 {opacity:P0}：启动/移动/结束 Alpha={before}/{moving}/{after}，一致。");
            }
            ThemeService.Configure(settings with { Acrylic = false });
            if (window.Background is not SolidColorBrush { Color.A: 255 }) throw new InvalidOperationException("关闭模糊后未恢复主题底色。");
            File.WriteAllLines(Path.Combine(output, "backdrop-verification.txt"), results);
        }
        finally { ThemeService.SetInteractiveMove(window, false); ThemeService.Configure(settings); }
    }
    private static byte MaskAlpha(ThemeBackdrop background)
    {
        var bitmap = new RenderTargetBitmap(16, 16, 96, 96, PixelFormats.Pbgra32); bitmap.Render(background);
        var pixel = new byte[4]; bitmap.CopyPixels(new Int32Rect(8, 8, 1, 1), pixel, 4, 0); return pixel[3];
    }
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
}
