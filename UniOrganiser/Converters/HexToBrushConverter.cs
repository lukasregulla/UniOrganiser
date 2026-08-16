using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace UniOrganiser.Converters;

public class HexToBrushConverter : IValueConverter
{
    // An optional ConverterParameter is an opacity in 0..1, used for the tinted
    // fill behind category pills (the pill's border and text use the same hex
    // at full strength).
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var opacity = ParseOpacity(parameter);

        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var colour = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(colour) { Opacity = opacity };
            }
            catch (FormatException)
            {
                // fall through to default below
            }
        }

        return new SolidColorBrush(Color.FromRgb(0x9D, 0x9D, 0x9D)) { Opacity = opacity };
    }

    private static double ParseOpacity(object? parameter)
    {
        if (parameter is double d) return d;
        if (parameter is string s
            && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return 1.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
