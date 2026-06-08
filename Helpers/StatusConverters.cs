using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PlustekBCR.Models;

namespace PlustekBCR.Helpers
{
    public class StatusToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is ProcessingStatus status)
            {
                return status switch
                {
                    PlustekBCR.Models.ProcessingStatus.Done => "AI Parsed",
                    PlustekBCR.Models.ProcessingStatus.Recognizing => "Processing...",
                    PlustekBCR.Models.ProcessingStatus.Pending => "Queued",
                    PlustekBCR.Models.ProcessingStatus.Manual => "Manual",
                    _ => "Unknown"
                };
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is PlustekBCR.Models.ProcessingStatus status)
            {
                return status switch
                {
                    PlustekBCR.Models.ProcessingStatus.Done => new SolidColorBrush(ColorHelper.FromArgb(255, 2, 122, 72)), // #027A48
                    PlustekBCR.Models.ProcessingStatus.Recognizing => new SolidColorBrush(ColorHelper.FromArgb(255, 0, 78, 235)), // #004EEB
                    PlustekBCR.Models.ProcessingStatus.Pending => new SolidColorBrush(ColorHelper.FromArgb(255, 102, 112, 133)), // #667085
                    PlustekBCR.Models.ProcessingStatus.Manual => new SolidColorBrush(ColorHelper.FromArgb(255, 2, 122, 72)), // #027A48
                    _ => new SolidColorBrush(Microsoft.UI.Colors.Gray)
                };
            }
            return new SolidColorBrush(Microsoft.UI.Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class StatusToForegroundBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is PlustekBCR.Models.ProcessingStatus status)
            {
                return status switch
                {
                    PlustekBCR.Models.ProcessingStatus.Done => new SolidColorBrush(ColorHelper.FromArgb(255, 2, 122, 72)), // #027A48
                    PlustekBCR.Models.ProcessingStatus.Recognizing => new SolidColorBrush(ColorHelper.FromArgb(255, 0, 78, 235)), // #004EEB
                    PlustekBCR.Models.ProcessingStatus.Pending => new SolidColorBrush(ColorHelper.FromArgb(255, 102, 112, 133)), // #667085
                    PlustekBCR.Models.ProcessingStatus.Manual => new SolidColorBrush(ColorHelper.FromArgb(255, 2, 122, 72)), // #027A48
                    _ => new SolidColorBrush(Microsoft.UI.Colors.Gray)
                };
            }
            return new SolidColorBrush(Microsoft.UI.Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class StatusToBackgroundBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is PlustekBCR.Models.ProcessingStatus status)
            {
                return status switch
                {
                    PlustekBCR.Models.ProcessingStatus.Done => new SolidColorBrush(ColorHelper.FromArgb(255, 236, 253, 243)), // #ECFDF3
                    PlustekBCR.Models.ProcessingStatus.Recognizing => new SolidColorBrush(ColorHelper.FromArgb(255, 239, 244, 255)), // #EFF4FF
                    PlustekBCR.Models.ProcessingStatus.Pending => new SolidColorBrush(ColorHelper.FromArgb(255, 248, 249, 250)), // #F8F9FA
                    PlustekBCR.Models.ProcessingStatus.Manual => new SolidColorBrush(ColorHelper.FromArgb(255, 236, 253, 243)), // #ECFDF3
                    _ => new SolidColorBrush(Microsoft.UI.Colors.Transparent)
                };
            }
            return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class StatusToProgressVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is PlustekBCR.Models.ProcessingStatus status)
            {
                return status == PlustekBCR.Models.ProcessingStatus.Recognizing ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class ByteArrayToImageSourceConverter : IValueConverter
    {
        private static readonly ConditionalWeakTable<byte[], BitmapImage> ImageCache = new();
        private static readonly BitmapImage PlaceholderImage = new(new Uri("ms-appx:///Assets/scanner_illustration.png"));

        public object? Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is byte[] bytes && bytes.Length > 0)
            {
                return ImageCache.GetValue(bytes, static key =>
                {
                    using var ms = new System.IO.MemoryStream(key, writable: false);
                    using var stream = ms.AsRandomAccessStream();
                    var image = new BitmapImage();
                    image.SetSource(stream);
                    return image;
                });
            }

            return PlaceholderImage;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool b)
            {
                return b ? Visibility.Visible : Visibility.Collapsed;
            }
            // Also handle null check for objects
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }


    public class DateTimeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DateTime dt)
            {
                return dt.ToString("yyyy/MM/dd HH:mm");
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class DateTimeToDateTimeOffsetConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DateTime dt)
            {
                return new DateTimeOffset(dt);
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is DateTimeOffset dto)
            {
                return dto.DateTime;
            }
            return DateTime.Now;
        }
    }
}
