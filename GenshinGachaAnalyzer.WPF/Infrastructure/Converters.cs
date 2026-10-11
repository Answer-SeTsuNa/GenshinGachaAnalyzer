using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GenshinGachaAnalyzer.WPF.Infrastructure;

public sealed class BoolVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type type, object parameter, CultureInfo culture) =>
        (value is true ^ parameter?.ToString() == "Invert") ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type type, object parameter, CultureInfo culture) => Binding.DoNothing;
}
