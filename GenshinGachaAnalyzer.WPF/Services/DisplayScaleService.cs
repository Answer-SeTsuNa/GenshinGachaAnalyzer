using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GenshinGachaAnalyzer.WPF.Services;

public static class DisplayScaleService
{
    // 先换算为逻辑像素，再决定额外缩放，避免在系统 200% DPI 下重复放大。
    public static (double Width, double Height, double Scale) ForWindow(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(handle, 2), ref info)) return (SystemParameters.WorkArea.Width, SystemParameters.WorkArea.Height, 1);
        double dpi = Math.Max(96, GetDpiForWindow(handle));
        double width = (info.Work.Right - info.Work.Left) * 96 / dpi;
        double height = (info.Work.Bottom - info.Work.Top) * 96 / dpi;
        double scale = width >= 3000 ? 1.5 : width >= 2400 ? 1.25 : 1;
        return (width, height, scale);
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr handle);
}
