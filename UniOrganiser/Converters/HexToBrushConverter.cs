using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace UniOrganiser.Converters;

public class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var colour = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(colour);
            }
            catch (FormatException)
            {
                // fall through to default below
            }
        }

        return new SolidColorBrush(Color.FromRgb(0x9D, 0x9D, 0x9D));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
