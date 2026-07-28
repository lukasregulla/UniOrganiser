using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace UniOrganiser.Converters;

// Multi-binding converter: values[0] = swatch hex, values[1] = currently selected hex.
// Returns the accent brush when they match, otherwise transparent - used to
// draw a selection ring around the chosen colour swatch.
public class ColourSelectedConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length == 2 && values[0] is string a && values[1] is string b &&
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
        {
            return new SolidColorBrush(Color.FromRgb(0x4F, 0xC1, 0xE9));
        }

        return Brushes.Transparent;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
