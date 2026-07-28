using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace UniOrganiser.Converters;

// Visible when the bound value is a non-null, non-empty string; Collapsed otherwise.
// Used to show/hide validation messages.
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasContent = value is string s ? !string.IsNullOrWhiteSpace(s) : value is not null;
        return hasContent ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
