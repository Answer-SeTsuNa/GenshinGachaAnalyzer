using System.ComponentModel;
using System.Windows;
using GenshinGachaAnalyzer.WPF.ViewModels;

namespace GenshinGachaAnalyzer.WPF.Views;
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent(); DataContext = viewModel;
        SourceInitialized += (_, _) => Services.ThemeService.ApplyWindow(this);
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
}
