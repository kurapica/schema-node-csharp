using System.ComponentModel;
using System.Windows.Input;

namespace SchemaNode.Avalonia.Man.Infrastructure;

/// <summary>
/// Shared command machinery. The WPF Man layer used Prism's <c>DelegateCommand</c>, including its
/// fluent <c>ObservesProperty</c> form; this port keeps that surface so the view models only need a
/// using-namespace change.
///
/// <c>ObservesProperty</c> subscribes to the owning view model's <see cref="INotifyPropertyChanged"/>
/// for the delegate target and re-evaluates <c>CanExecute</c> on any change. Prism watched the exact
/// property name; observing every change is a superset and keeps the enabled state fresh without
/// expression-tree parsing.
/// </summary>
public abstract class DelegateCommandBase : ICommand
{
    private readonly HashSet<INotifyPropertyChanged> _observed = new();
    private EventHandler? _canExecuteChanged;

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged
    {
        add => _canExecuteChanged += value;
        remove => _canExecuteChanged -= value;
    }

    /// <summary>Whether the command can run with the supplied parameter.</summary>
    protected abstract bool CanExecuteInternal(object? parameter);

    /// <summary>Runs the command with the supplied parameter.</summary>
    protected abstract void ExecuteInternal(object? parameter);

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => CanExecuteInternal(parameter);

    /// <inheritdoc />
    public void Execute(object? parameter) => ExecuteInternal(parameter);

    /// <summary>Re-evaluates CanExecute and notifies bound controls.</summary>
    public void RaiseCanExecuteChanged() => _canExecuteChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Subscribes to the delegate owner's property changes so CanExecute is re-evaluated. Derived
    /// commands expose the fluent, strongly-typed <c>ObservesProperty</c> on top of this.
    /// </summary>
    protected void ObserveProperty<T>(Func<T> property)
    {
        ArgumentNullException.ThrowIfNull(property);
        if (property.Target is INotifyPropertyChanged owner && _observed.Add(owner))
            owner.PropertyChanged += OnObservedPropertyChanged;
    }

    private void OnObservedPropertyChanged(object? sender, PropertyChangedEventArgs e) => RaiseCanExecuteChanged();
}

/// <summary>Parameter-less command with an optional CanExecute predicate.</summary>
public sealed class DelegateCommand : DelegateCommandBase
{
    private readonly Func<bool>? _canExecute;
    private readonly Action _execute;

    /// <summary>Creates a command.</summary>
    public DelegateCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>Fluent form: re-evaluates CanExecute when the owning view model changes.</summary>
    public DelegateCommand ObservesProperty<T>(Func<T> property)
    {
        ObserveProperty(property);
        return this;
    }

    /// <inheritdoc />
    protected override bool CanExecuteInternal(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <inheritdoc />
    protected override void ExecuteInternal(object? parameter) => _execute();
}

/// <summary>Command that receives the bound parameter.</summary>
public sealed class DelegateCommand<T> : DelegateCommandBase
{
    private readonly Func<T?, bool>? _canExecute;
    private readonly Action<T?> _execute;

    /// <summary>Creates a command.</summary>
    public DelegateCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>Fluent form: re-evaluates CanExecute when the owning view model changes.</summary>
    public DelegateCommand<T> ObservesProperty<TProperty>(Func<TProperty> property)
    {
        ObserveProperty(property);
        return this;
    }

    /// <inheritdoc />
    protected override bool CanExecuteInternal(object? parameter)
        => _canExecute?.Invoke(Cast(parameter)) ?? true;

    /// <inheritdoc />
    protected override void ExecuteInternal(object? parameter) => _execute(Cast(parameter));

    private static T? Cast(object? parameter) => parameter is T value ? value : default;
}
