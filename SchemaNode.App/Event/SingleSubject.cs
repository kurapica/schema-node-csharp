using System.Reactive.Disposables;
using System.Reactive.Subjects;

namespace SchemaNode.Event;

/// <summary>
/// A subject that resets observers after each OnNext, OnError.
/// </summary>
public sealed class SingleSubject<T> : ISubject<T>, IDisposable
{
    private readonly Lock _gate = new();
    private Subject<T> _inner = new();
    private bool _isDisposed;

    /// <summary>
    /// Send next value to current subscribers, then reset the internal subject.
    /// </summary>
    public void OnNext(T value)
    {
        Subject<T>? toNotify = null;
        lock (_gate)
        {
            if (_isDisposed) return;

            // Capture the current subject and replace with a new one
            toNotify = _inner;
            _inner = new Subject<T>();
        }

        // Notify outside the lock to avoid deadlocks
        toNotify.OnNext(value);
        toNotify.OnCompleted();
        toNotify.Dispose();
    }

    /// <summary>
    /// Send error to current subscribers, then reset the internal subject.
    /// </summary>
    public void OnError(Exception error)
    {
        Subject<T>? toNotify = null;
        lock (_gate)
        {
            if (_isDisposed) return;
            toNotify = _inner;
        }

        toNotify.OnError(error);
    }

    /// <summary>
    /// Completes all current subscribers, then resets the subject.
    /// </summary>
    public void OnCompleted()
    {
        Subject<T>? toNotify = null;
        lock (_gate)
        {
            if (_isDisposed) return;

            toNotify = _inner;
            _inner = new Subject<T>();
        }

        toNotify.OnCompleted();
        toNotify.Dispose();
    }

    /// <summary>
    /// Subscribe to the current subject instance.
    /// </summary>
    public IDisposable Subscribe(IObserver<T> observer)
    {
        if (observer == null) throw new ArgumentNullException(nameof(observer));

        lock (_gate)
        {
            if (_isDisposed)
            {
                observer.OnCompleted();
                return Disposable.Empty;
            }

            return _inner.Subscribe(observer);
        }
    }

    /// <summary>
    /// Dispose and clean up all resources.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _inner.Dispose();
        }
    }
}
