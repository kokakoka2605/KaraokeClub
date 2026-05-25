using System;
using System.Globalization;
using System.Windows.Data;

namespace KaraokeClub.Converters
{
    public class EnumToBoolConverter : IValueConverter, IMultiValueConverter
    {
        // Старый метод (используется в других местах)
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return value.ToString() == parameter.ToString();
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();

        // Новый метод для RadioButton столов: values[0] = SelectedTable, values[1] = текущий стол
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return false;
            return Equals(values[0], values[1]);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}