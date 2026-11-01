using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ClipFlow.Core.Models;

namespace ClipFlow.App.Converters;

public class BooleanToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is bool val && val;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility vis && vis == Visibility.Visible;
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool hasValue = value != null && (value is not string s || !string.IsNullOrWhiteSpace(s));
        if (Invert) hasValue = !hasValue;
        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class EqualityToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class ColorHexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(color);
            }
            catch { }
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class ContentTypeToBadgeColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ContentType type)
        {
            return type switch
            {
                ContentType.Text => new SolidColorBrush(Color.FromRgb(59, 130, 246)),    // Blue
                ContentType.Code => new SolidColorBrush(Color.FromRgb(168, 85, 247)),   // Purple
                ContentType.Url => new SolidColorBrush(Color.FromRgb(14, 165, 233)),    // Sky Blue
                ContentType.Color => new SolidColorBrush(Color.FromRgb(236, 72, 153)),  // Pink
                ContentType.Image => new SolidColorBrush(Color.FromRgb(34, 197, 94)),   // Emerald Green
                ContentType.FileDrop => new SolidColorBrush(Color.FromRgb(249, 115, 22)),// Amber / Orange
                ContentType.Email => new SolidColorBrush(Color.FromRgb(20, 184, 166)),  // Teal
                ContentType.Phone => new SolidColorBrush(Color.FromRgb(244, 63, 94)),   // Rose
                _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))
            };
        }
        return new SolidColorBrush(Color.FromRgb(100, 116, 139));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
