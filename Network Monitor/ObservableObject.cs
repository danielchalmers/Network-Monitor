using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Network_Monitor;

public class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool Set<T>(
        ref T field,
        T newValue = default,
        bool checkEquality = true,
        [CallerMemberName] string propertyName = null)
    {
        if (checkEquality && EqualityComparer<T>.Default.Equals(field, newValue))
            return false;

        field = newValue;
        RaisePropertyChanged(propertyName);
        return true;
    }
}