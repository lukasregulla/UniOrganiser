using System.Globalization;
using System.Windows.Data;

namespace UniOrganiser.Converters;

// Multi-binding converter: values[0] = this cell's date, values[1] = the selected date.
// Returns true when they are the same day, so a DataTrigger can apply the theme's
// accent brush to the cell border instead of the converter hardcoding a colour.
public class IsSelectedDayConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        return values.Length == 2 && values[0] is DateTime a && values[1] is DateTime b && a.Date == b.Date;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
