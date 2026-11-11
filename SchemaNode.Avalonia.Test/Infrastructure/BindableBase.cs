using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SchemaNode.Avalonia.Test.Infrastructure;

/// <summary>
/// Minimal INotifyPropertyChanged base. The WPF harness used Prism's <c>BindableBase</c>; the
/// Avalonia port keeps the same surface so the sample view model stays platform-neutral.
/// </summary>
public abstract class BindableBase : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Assigns <paramref name="value"/> when it differs and raises the change notification.</summary>
    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
        storage = value;
        RaisePropertyChanged(propertyName);
        return true;
    }

    /// <summary>Raises a property change notification for computed properties.</summary>
    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
