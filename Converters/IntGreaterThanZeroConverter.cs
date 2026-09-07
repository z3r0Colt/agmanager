using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AgApp.Converters;

public class IntGreaterThanZeroConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool result = value is int i && i > 0;
        if (Invert) result = !result;
        return targetType == typeof(bool) ? result : result ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
