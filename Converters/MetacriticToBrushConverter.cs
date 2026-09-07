using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AgApp.Converters;

public class MetacriticToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not int score || score <= 0)
            return new SolidColorBrush(Colors.Transparent);
        return score >= 75 ? new SolidColorBrush(Color.FromRgb(0x3d, 0x9a, 0x3e))
             : score >= 50 ? new SolidColorBrush(Color.FromRgb(0xb5, 0xa0, 0x20))
             : new SolidColorBrush(Color.FromRgb(0xc0, 0x39, 0x2b));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
