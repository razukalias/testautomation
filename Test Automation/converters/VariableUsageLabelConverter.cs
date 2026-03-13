using System;
using System.Windows;
using System.Windows.Data;

namespace Test_Automation.Converters
{
    public class VariableUsageLabelConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var variableName = values.Length > 0 ? values[0]?.ToString() ?? string.Empty : string.Empty;
            if (Application.Current?.MainWindow is not MainWindow window)
            {
                return string.Empty;
            }

            return window.GetVariableUniquenessLabel(variableName);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
