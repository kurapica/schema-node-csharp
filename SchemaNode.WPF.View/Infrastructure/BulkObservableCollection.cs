using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SchemaNode.WPF.View;

/// <summary>
/// An observable collection that supports bulk replacement.
///
/// A large collection refresh is published as a single Reset notification instead of one
/// Add/Remove per item, so an ItemsControl does not re-run layout for every element.
/// </summary>
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotifications;

    /// <summary>
    /// Replaces all items in one Reset notification. Call this on the UI thread.
    /// </summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        List<T> next = items as List<T> ?? items.ToList();

        _suppressNotifications = true;
        try
        {
            Items.Clear();
            foreach (T item in next)
                Items.Add(item);
        }
        finally
        {
            _suppressNotifications = false;
        }

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
    }

    /// <inheritdoc />
    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotifications)
            base.OnCollectionChanged(e);
    }
}
