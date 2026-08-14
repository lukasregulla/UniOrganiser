using System.Globalization;
using System.Windows.Data;

namespace UniOrganiser.Converters;

// Multi-binding converter: values[0] = swatch hex, values[1] = currently selected hex.
// Returns true when they match, so a DataTrigger can draw the selection ring with the
// theme's accent brush instead of the converter hardcoding a colour.
public class ColourSelectedConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        return values.Length == 2 && values[0] is string a && values[1] is string b &&
               string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
