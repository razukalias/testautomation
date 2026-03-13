using System;
using System.Windows;
using System.Windows.Data;

namespace Test_Automation.Converters
{
    public class VariableSettingUsageTooltipConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var key = values.Length > 0 ? values[0]?.ToString() ?? string.Empty : string.Empty;
            var value = values.Length > 1 ? values[1]?.ToString() ?? string.Empty : string.Empty;
            if (!MainWindow.IsVariableSettingKey(key))
            {
                return string.Empty;
            }

            if (Application.Current?.MainWindow is not MainWindow window)
            {
                return string.Empty;
            }

            return window.GetVariableUsageTooltip(value);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
