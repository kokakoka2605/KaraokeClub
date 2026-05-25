using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KaraokeClub.Converters
{
    /// <summary>
    /// Возвращает Visible, если значение равно ConverterParameter, иначе Collapsed.
    /// Используется для показа иконки 🔒 только у строк с ролью "admin".
    /// </summary>
    public class EqToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var str = value?.ToString() ?? "";
            var param = parameter?.ToString() ?? "";
            return string.Equals(str, param, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
