using System;
using System.Globalization;
using System.Windows.Data;

namespace Network_Monitor;

/// <summary>
/// Maps an enum value to true when it equals the converter parameter, so a group of menu items can show which one of them a single enum setting is set to.
/// </summary>
public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not null && parameter is string name && value.ToString() == name;

    // The menu items set the value on click instead, since unchecking the checked one would otherwise leave nothing checked.
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
