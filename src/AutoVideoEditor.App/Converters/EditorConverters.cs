using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AutoVideoEditor.Core.Enums;

namespace AutoVideoEditor.App.Converters;

public class JobStatusToVietnameseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is JobStatus status)
        {
            return status switch
            {
                JobStatus.Pending => "Đang chờ",
                JobStatus.AnalyzingVoice => "Đang phân tích giọng đọc...",
                JobStatus.DetectingSilence => "Đang phát hiện khoảng lặng...",
                JobStatus.AnalyzingVideo => "Đang phân tích video...",
                JobStatus.BuildingTimeline => "Đang xây dựng timeline...",
                JobStatus.Rendering => "Đang mã hóa video...",
                JobStatus.Completed => "✓ Hoàn thành",
                JobStatus.Failed => "✕ Thất bại",
                JobStatus.Canceled => "⊘ Đã hủy",
                JobStatus.Paused => "⏸ Tạm dừng",
                _ => status.ToString()
            };
        }
        return "N/A";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class JobStatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(16, 185, 129));
    private static readonly SolidColorBrush CyanBrush = new(Color.FromRgb(6, 182, 212));
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(239, 68, 68));
    private static readonly SolidColorBrush AmberBrush = new(Color.FromRgb(245, 158, 11));
    private static readonly SolidColorBrush GrayBrush = new(Color.FromRgb(113, 113, 122));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is JobStatus status)
        {
            return status switch
            {
                JobStatus.Completed => GreenBrush,
                JobStatus.Rendering or JobStatus.AnalyzingVoice or JobStatus.DetectingSilence or JobStatus.AnalyzingVideo or JobStatus.BuildingTimeline => CyanBrush,
                JobStatus.Failed => RedBrush,
                JobStatus.Paused => AmberBrush,
                JobStatus.Canceled => GrayBrush,
                _ => GrayBrush
            };
        }
        return GrayBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class LogLevelToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(16, 185, 129));
    private static readonly SolidColorBrush CyanBrush = new(Color.FromRgb(56, 189, 248));
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(248, 113, 113));
    private static readonly SolidColorBrush AmberBrush = new(Color.FromRgb(251, 191, 36));
    private static readonly SolidColorBrush GrayBrush = new(Color.FromRgb(161, 161, 170));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string level)
        {
            return level.ToUpperInvariant() switch
            {
                "SUCCESS" => GreenBrush,
                "INFO" => CyanBrush,
                "WARN" or "WARNING" => AmberBrush,
                "ERROR" => RedBrush,
                _ => GrayBrush
            };
        }
        return GrayBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class LogLevelToBgBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBg = new(Color.FromArgb(40, 16, 185, 129));
    private static readonly SolidColorBrush CyanBg = new(Color.FromArgb(30, 6, 182, 212));
    private static readonly SolidColorBrush RedBg = new(Color.FromArgb(45, 239, 68, 68));
    private static readonly SolidColorBrush AmberBg = new(Color.FromArgb(35, 245, 158, 11));
    private static readonly SolidColorBrush GrayBg = new(Color.FromArgb(25, 113, 113, 122));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string level)
        {
            return level.ToUpperInvariant() switch
            {
                "SUCCESS" => GreenBg,
                "INFO" => CyanBg,
                "WARN" or "WARNING" => AmberBg,
                "ERROR" => RedBg,
                _ => GrayBg
            };
        }
        return GrayBg;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return false;
    }
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool flag = false;

        if (value is bool b)
        {
            flag = b;
        }
        else if (value is int i)
        {
            flag = i > 0;
        }
        else if (value is long l)
        {
            flag = l > 0;
        }
        else if (value is ICollection coll)
        {
            flag = coll.Count > 0;
        }
        else if (value != null)
        {
            flag = true;
        }

        if (parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class DoubleToPercentStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            return $"{d:F1}%";
        }
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class FormatEtaConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeSpan ts)
        {
            return ts.Hours > 0 
                ? $"Còn {ts.Hours}h {ts.Minutes:D2}m {ts.Seconds:D2}s" 
                : $"Còn {ts.Minutes:D2}:{ts.Seconds:D2}";
        }
        return "--:--";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
