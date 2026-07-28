using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace UniOrganiser.Converters;

// Multi-binding converter: values[0] = this cell's date, values[1] = the selected date.
// Returns the accent brush when they match, otherwise the default border colour.
public class SelectedDayBorderConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length == 2 && values[0] is DateTime a && values[1] is DateTime b && a.Date == b.Date)
        {
            return new SolidColorBrush(Color.FromRgb(0x4F, 0xC1, 0xE9));
        }

        return new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
