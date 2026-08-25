using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace IndoTweaks.Helpers;

/// <summary>True -> Collapsed, False -> Visible. Used for "show this warning unless X is true" banners.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
