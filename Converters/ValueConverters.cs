using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Pomodoro.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool val = value is bool b && b;
            
            // Handle parameter string "Inverse" as well
            if (Invert || (parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase)))
            {
                val = !val;
            }

            return val ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
