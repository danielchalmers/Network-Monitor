using System;
using System.Globalization;
using System.Windows.Data;

namespace Network_Monitor;

/// <summary>
/// Maps an enum value to true when it equals the converter parameter, letting a group of checkable menu items act as radio buttons bound to a single enum setting.
/// </summary>
public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not null && parameter is string name && value.ToString() == name;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true && parameter is string name ? Enum.Parse(targetType, name) : Binding.DoNothing;
}
