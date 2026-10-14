using System.Windows;
using System.Runtime.CompilerServices;
using GenshinGachaAnalyzer.Core.Helpers;
using GenshinGachaAnalyzer.Core.Services;
using GenshinGachaAnalyzer.WPF.Infrastructure;

namespace GenshinGachaAnalyzer.WPF;

/// <summary>在 WPF 资源和窗口初始化之前记录启动位置，不记录命令行或认证信息。</summary>
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        bool verify = args.Length >= 2 && args[0] is "--verify-startup" or "--verify-ui";
        string directory = verify ? Path.GetFullPath(args[1]) : AppPaths.DataDirectory;
        if (verify && (string.Equals(directory.TrimEnd(Path.DirectorySeparatorChar), AppPaths.DataDirectory,
                StringComparison.OrdinalIgnoreCase) || File.Exists(Path.Combine(directory, "gacha.db"))))
        {
            Environment.ExitCode = 2; return;
        }
        DiagnosticLog.Configure(Path.Combine(directory, "logs"));
        DiagnosticLog.Info($"启动入口：{typeof(Program).Assembly.GetName().Version!.ToString(3)}；进程 {Environment.ProcessId}；{Environment.ProcessPath}");
        DiagnosticLog.Info($"进程完整性级别：{ProcessSecurity.IntegrityLevel()?.ToString() ?? "未知"}；数据目录：{directory}");
        try
        {
            StartupCompatibility.Initialize();
            RunApplication();
        }
        catch (Exception error)
        {
            DiagnosticLog.Error("窗口初始化前启动失败", error);
            if (!verify) MessageBox.Show(ErrorMessages.Readable(error) + "\n\n诊断日志：" + DiagnosticLog.CurrentPath,
                "启动未完成", MessageBoxButton.OK, MessageBoxImage.Warning);
            Environment.ExitCode = 1;
        }
    }

    // 避免 JIT 提前初始化 WPF 渲染依赖，兼容策略必须先执行。
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunApplication()
    {
        DiagnosticLog.Info("启动阶段：构造应用");
        var app = new App();
        DiagnosticLog.Info("启动阶段：读取 WPF 资源");
        app.InitializeComponent();
        DiagnosticLog.Info("启动阶段：进入消息循环");
        Environment.ExitCode = app.Run();
    }
}
