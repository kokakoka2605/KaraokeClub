using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KaraokeClub.Converters
{
    /// <summary>
    /// Возвращает Visible когда строка null / пустая (используется для заглушки "Картинка не выбрана").
    /// Возвращает Collapsed когда строка заполнена.
    /// </summary>
    public class NullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
