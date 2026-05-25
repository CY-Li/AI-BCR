using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
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
        public object? Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is byte[] bytes)
            {
                // Note: In a real app, you might want to cache these or use a non-UI thread
                // but for sample data this is fine.
                using var ms = new System.IO.MemoryStream(bytes);
                var image = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                // This is a bit tricky in WinUI 3 for desktop without a XamlRoot 
                // but since it's a converter during binding it should be okay.
                // However, the standard way is to use SetSourceAsync if possible.
                // For simplicity here, we'll try this.
                var stream = ms.AsRandomAccessStream();
                image.SetSource(stream);
                return image;
            }
            return new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/scanner_illustration.png"));
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
