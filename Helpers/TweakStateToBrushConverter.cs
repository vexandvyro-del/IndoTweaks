using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using IndoTweaks.Models;

namespace IndoTweaks.Helpers;

/// <summary>
/// Maps TweakState -> status color, matching the Green/Yellow/Red convention
/// used across the Dashboard and Tweaks tabs.
/// </summary>
public sealed class TweakStateToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TweakState state) return Brushes.Gray;

        var key = state switch
        {
            TweakState.Applied => "StatusGoodBrush",
            TweakState.PartiallyApplied => "StatusWarnBrush",
            TweakState.NotApplied => "StatusBadBrush",
            _ => null,
        };

        return key is not null && Application.Current.TryFindResource(key) is Brush brush
            ? brush
            : Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
