using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace KaraokeClub.Converters
{
    /// <summary>
    /// Конвертирует строку-путь к файлу изображения в BitmapImage.
    /// Если путь null / файл не найден — возвращает null (показывается заглушка).
    /// </summary>
    public class ImagePathConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string path || string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                // Поддерживаем абсолютный и относительный пути
                if (!Path.IsPathRooted(path))
                    path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);

                if (!File.Exists(path)) return null;

                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.DecodePixelWidth = 240;   // ограничиваем декодирование для экономии памяти
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
