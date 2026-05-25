using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace KaraokeClub.Converters
{
    public class RowNumberConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is DataGrid dataGrid && values[1] != null && values[1] != DependencyProperty.UnsetValue)
            {
                var index = dataGrid.Items.IndexOf(values[1]);
                if (index >= 0)
                    return (index + 1).ToString();
            }
            return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}