using System.Diagnostics;
using System.Runtime.InteropServices;
using GenshinGachaAnalyzer.Core.Services;

namespace GenshinGachaAnalyzer.WPF.Infrastructure;

/// <summary>在加载 WPF/Direct3D 前选择叠加层兼容路径，仅影响本进程。</summary>
internal static class StartupCompatibility
{
    public static bool OverlayDetected { get; private set; }
    public static bool ExtensionPointsDisabled { get; private set; }

    public static void Initialize()
    {
        var overlays = Process.GetProcessesByName("GamePP");
        try { OverlayDetected = overlays.Length > 0; }
        finally { foreach (var process in overlays) process.Dispose(); }
        if (!OverlayDetected) return;

        // 禁用旧式全局窗口钩子/AppInit 扩展点，不卸载已加载 DLL，不修改其他软件。
        uint flags = 1;
        ExtensionPointsDisabled = SetProcessMitigationPolicy(6, ref flags, (nuint)sizeof(uint));
        int error = ExtensionPointsDisabled ? 0 : Marshal.GetLastPInvokeError();
        DiagnosticLog.Info($"检测到游戏加加：启用软件渲染兼容；旧式扩展点禁用 {ExtensionPointsDisabled}。");
        if (!ExtensionPointsDisabled)
            DiagnosticLog.Info($"扩展点策略不可用，系统错误 {error}；继续软件渲染。");
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessMitigationPolicy(int policy, ref uint flags, nuint size);
}
