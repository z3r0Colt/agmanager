using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AgApp.Models;

namespace AgApp.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is DownloadStatus status ? status switch
        {
            DownloadStatus.Downloading => new SolidColorBrush(Color.FromRgb(99, 102, 241)),
            DownloadStatus.Completed => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
            DownloadStatus.Failed => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
            DownloadStatus.Paused => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
            DownloadStatus.Extracting => new SolidColorBrush(Color.FromRgb(6, 182, 212)),
            _ => new SolidColorBrush(Color.FromRgb(156, 163, 175))
        } : Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
