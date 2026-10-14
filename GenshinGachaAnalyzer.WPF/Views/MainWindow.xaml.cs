using System.Windows;
using System.Windows.Interop;
using GenshinGachaAnalyzer.WPF.ViewModels;

namespace GenshinGachaAnalyzer.WPF.Views;
public partial class MainWindow : Window
{
    private HwndSource? _source;
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent(); DataContext = viewModel;
        SourceInitialized += (_, _) =>
        {
            _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            _source?.AddHook(WindowMessage);
            Services.ThemeService.ApplyWindow(this);
        };
        Closed += (_, _) => { _source?.RemoveHook(WindowMessage); _source = null; };
        SourceInitialized += (_, _) =>
        {
            var display = Services.DisplayScaleService.ForWindow(this); viewModel.SetAutomaticScale(display.Scale);
            Width = Math.Min(display.Width * .92, 1640 * display.Scale); Height = Math.Min(display.Height * .92, 1020 * display.Scale);
        };
        DpiChanged += (_, _) => viewModel.SetAutomaticScale(Services.DisplayScaleService.ForWindow(this).Scale);
        Closing += (_, e) =>
        {
            if (viewModel.IsBusy) { viewModel.CancelCommand.Execute(null); e.Cancel = true; }
        };
    }
    private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0231) Services.ThemeService.SetInteractiveMove(this, true); // WM_ENTERSIZEMOVE
        else if (message == 0x0232) Services.ThemeService.SetInteractiveMove(this, false); // WM_EXITSIZEMOVE（含取消拖动）
        return IntPtr.Zero; // 保留系统移动、缩放、贴靠与标题栏按钮处理。
    }
}
