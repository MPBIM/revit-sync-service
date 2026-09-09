using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using RevitSyncService.Core.Models;

namespace RevitSyncService.UI.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ProjectStatus status)
            {
                return status switch
                {
                    ProjectStatus.Waiting => Brushes.Gray,
                    ProjectStatus.Queued => new SolidColorBrush(Color.FromRgb(147, 130, 220)),
                    ProjectStatus.Running => Brushes.DodgerBlue,
                    ProjectStatus.Completed => new SolidColorBrush(Color.FromRgb(22, 163, 74)),
                    ProjectStatus.Failed => new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                    ProjectStatus.Cancelled => new SolidColorBrush(Color.FromRgb(217, 119, 6)),
                    _ => Brushes.Gray
                };
            }
            return Brushes.Gray;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class LogLevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
            {
                return level switch
                {
                    LogLevel.Info => Brushes.DimGray,
                    LogLevel.Success => new SolidColorBrush(Color.FromRgb(22, 163, 74)),
                    LogLevel.Warning => new SolidColorBrush(Color.FromRgb(217, 119, 6)),
                    LogLevel.Error => new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                    _ => Brushes.DimGray
                };
            }
            return Brushes.DimGray;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool invert = parameter?.ToString() == "Invert";
            bool boolVal = value is bool b && b;
            if (invert) boolVal = !boolVal;
            return boolVal ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class DateTimeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dt)
                return dt.ToString("dd.MM.yyyy HH:mm");
            return "—";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !string.IsNullOrWhiteSpace(value?.ToString())
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}