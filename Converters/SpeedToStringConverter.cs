using System.Globalization;
using System.Windows.Data;

namespace AgApp.Converters;

public class SpeedToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double bps) return "0 KB/s";
        return bps switch
        {
            < 1024 => $"{bps:F0} B/s",
            < 1024 * 1024 => $"{bps / 1024.0:F1} KB/s",
            _ => $"{bps / 1024.0 / 1024.0:F2} MB/s"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
