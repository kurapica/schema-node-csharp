using Microsoft.Extensions.DependencyInjection;
using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Struct;
using SchemaNode.Runtime;
using SchemaNode.Struct;
using System.Collections.Concurrent;
using System.Reactive;
using System.Reactive.Subjects;
using System.Reflection;
using System.Text.Json.Nodes;

// ReSharper disable InconsistentNaming
// ReSharper disable VirtualMemberCallInConstructor
// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract

namespace SchemaNode.Node;

/// <summary>
/// The data node interface, which represents a node in the data structure. It can be a value node, an array node, or a struct node.
/// </summary>
public abstract class DataNode : IValueAccess, IDisposable
{
    #region Fields

    /// <summary>
    /// The schema context, optional. Detached values (like function return values) are not bound to
    /// any context and behave as non-full-mode nodes.
    /// </summary>
    public ISchemaContext? Context { get; init; }


    /// <summary>
    /// Whether enable full data node mode
    /// </summary>
    public bool FullMode => Context is SchemaContext ctx && ctx.FullMode;

    /// <inheritdoc/>
    public Guid Id { get; } = Guid.CreateVersion7();

    /// <inheritdoc/>
    public IValueTypeAccess Type { get; init; } = null!;

    /// <summary>
    /// The property provider
    /// </summary>
    private readonly List<IPropertyProvider> _propertyProviders;

    /// <summary>
    /// The property providers, exposed to derived nodes for cloning and child creation
    /// </summary>
    protected IReadOnlyList<IPropertyProvider> PropertyProviders => _propertyProviders;

    /// <summary>
    /// Adds a property provider to the node, which is used by composite nodes to forward the field properties
    /// </summary>
    public void AddPropertyProvider(IPropertyProvider? provider)
    {
        if (provider is null || _propertyProviders.Contains(provider)) return;
        _propertyProviders.Add(provider);
    }

    /// <summary>
    /// Violated Constraints
    /// </summary>
    private List<IConstraintProperty>? _violated;

    /// <summary>
    /// Avoid double validation
    /// </summary>
    private CancellationTokenSource? _validated;

    /// <summary>
    /// The override properties
    /// </summary>
    private ConcurrentDictionary<Type, List<PropertyRecord>>? _props;

    /// <summary>
    /// The data observable
    /// </summary>
    private Subject<(IValueAccess, object?)>? _dataOb;

    /// <summary>
    /// The violated observable
    /// </summary>
    private Subject<(IValueAccess, bool)>? _violatedOb;

    /// <summary>
    /// The self property observable
    /// </summary>
    private ConcurrentDictionary<Type, Subject<object>>? _selfPropObs;

    /// <summary>
    /// The property observable
    /// </summary>
    private ConcurrentDictionary<Type, Subject<object>>? _propObs;

    /// <summary>
    /// The move observable
    /// </summary>
    private Subject<IValueAccess>? _moveObs;

    /// <summary>
    /// The record subscriptions
    /// </summary>
    private ConcurrentDictionary<object, ConcurrentBag<IDisposable>>? _subs;

    /// <summary>
    /// The cached property setters for runtime property constructors
    /// </summary>
    private static readonly ConcurrentDictionary<Type, MethodInfo> _setters = new();

    /// <summary>
    /// The cached factories for runtime property instances. A property class normally declares a
    /// constructor taking the source node; declaration-only classes (`class X : Property&lt;T&gt;;`)
    /// only have the implicit parameterless constructor and are created without the source argument
    /// (the record still carries <see cref="PropertyRecord.Source"/>).
    /// </summary>
    private static readonly ConcurrentDictionary<Type, ObjectFactory> _propertyFactories = new();

    /// <summary>
    /// The cached data node constructors for schema declared node types
    /// </summary>
    private static readonly ConcurrentDictionary<Type, ConstructorInfo> _nodeCtors = new();

    #endregion

    #region Constructor

    public DataNode(ISchemaContext? context, IValueTypeAccess type, object? value, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
    {
        Context = context;
        Type = type;
        Parent = parent;
        _propertyProviders = [.. propertyProviders];

        if (value is not null)
        {
            TrySetValue(value);
            if (FullMode)
                Confirm();
        }

        // init properties effect, chilren node should be applied by its parent after confirmed
        if (FullMode && parent == null)
            ApplyPropertyEffects();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        // clear property effect
        if (FullMode)
            foreach (var prop in GetProperties<IProperty>())
                prop.Clear(this);

        _validated?.Cancel();

        _dataOb?.Dispose();
        _dataOb = null;

        _violatedOb?.Dispose();
        _violatedOb = null;

        _moveObs?.Dispose();
        _moveObs = null;

        if (_subs is not null)
        {
            foreach (var s in _subs.Values)
                foreach (var d in s)
                    d.Dispose();
            _subs.Clear();
            _subs = null;
        }

        if (_selfPropObs is not null)
        {
            foreach (var s in _selfPropObs.Values)
                s.Dispose();
            _selfPropObs.Clear();
            _selfPropObs = null;
        }

        if (_propObs is not null)
        {
            foreach (var s in _propObs.Values)
                s.Dispose();
            _propObs.Clear();
            _propObs = null;
        }
    }

    #endregion

    #region ShortCuts

    /// <summary>
    /// The data node is required
    /// </summary>
    public bool Require => GetPropertyValue<Require, bool>();

    /// <summary>
    /// The data node is visible
    /// </summary>
    public bool Visible => !GetPropertyValue<InVisible, bool>() && GetProperty<Visible>()?.Value != false;

    /// <summary>
    /// Whether the node and every ancestor in the semantic tree are visible.
    /// Hidden nodes do not take part in validation or submission, even when their own
    /// visibility property is unset because an ancestor hides them.
    /// </summary>
    public bool VisibleInTree
    {
        get
        {
            IValueAccess? current = this;
            while (current is DataNode node)
            {
                if (!node.Visible) return false;
                current = node.Parent;
            }
            return true;
        }
    }

    /// <summary>
    /// Whether the node is display-only
    /// </summary>
    public bool DisplayOnly => GetPropertyValue<DisplayOnly, bool>();

    /// <summary>
    /// Whether the node diable constraint
    /// </summary>
    public bool DisableConstraint => GetPropertyValue<DisableConstraint, bool>() || Parent is DataNode p && p.DisableConstraint;

    #endregion

    #region Value Access

    /// <inheritdoc/>
    public abstract bool IsEmpty { get; }

    /// <inheritdoc/>
    public abstract bool TryGetOriginal(Type type, out object? value);

    /// <inheritdoc/>
    public virtual bool TryGetOriginal<T>(out T? value)
    {
        bool isEmpty = IsEmpty;
        if (isEmpty || !TryGetOriginal(typeof(T), out object? obj))
        {
            value = default(T?);
            return isEmpty;
        }
        value = (T?)obj;
        return true;
    }

    /// <inheritdoc/>
    public abstract bool TrySetValue<T>(T? value);

    /// <inheritdoc/>
    public abstract bool TryGetValue(Type type, out object? value);

    /// <inheritdoc/>
    public virtual void ClearValue() => TrySetValue<object>(null);

    /// <inheritdoc/>
    public virtual bool TryGetValue<T>(out T? value)
    {
        bool isEmpty = IsEmpty;
        if (isEmpty || !TryGetValue(typeof(T), out object? obj))
        {
            value = default(T?);
            return isEmpty;
        }
        value = (T?)obj;
        return true;
    }

    /// <inheritdoc/>
    public T? GetOriginal<T>() => TryGetOriginal<T>(out T? value) ? value : default(T?);

    /// <inheritdoc/>
    public object? GetOriginal(Type type) => TryGetOriginal(type, out object? value) ? value : null;

    /// <inheritdoc/>
    public T? GetValue<T>() => TryGetValue(out T? value) ? value : default(T?);

    /// <inheritdoc/>
    public object? GetValue(Type type) => TryGetValue(type, out object? value) ? value : null;

    /// <summary>
    /// Clone the value as a detached copy. The copy is not bound to any schema context, so it does not
    /// depend on the lifetime of the context the source node was created from.
    /// </summary>
    public abstract IValueAccess Clone();

    /// <summary>
    /// Clone the value bound to the given schema context
    /// </summary>
    public virtual IValueAccess Clone(SchemaContext? context) => Clone();

    /// <summary>
    /// Whether the data changed
    /// </summary>
    public abstract bool Changed { get;  }

    /// <summary>
    /// Confirm the changes and save to original
    /// </summary>
    public abstract void Confirm();

    /// <summary>
    /// Reset the value from original
    /// </summary>
    public abstract void Reset();

    /// <summary>
    /// Init th data node value, should only be called by parent, like array add new row
    /// </summary>
    public virtual async Task InitAsync()
    {
        var init = GetProperty<Init>();
        if (init is null || init.Value?.Func is null || Context is not SchemaContext ctx) return;
        var func = await ctx.GetNodeTypeAsync<FunctionType>(init.Value.Func).ConfigureAwait(false);
        if (func is null) return;
        try
        {
            var source = init.Source ?? this;
            var value = await func.CallAsync<object>(ctx, init.Value.Args.Select(a =>
            {
                if (string.IsNullOrWhiteSpace(a.Source)) return a.Value;
                return source.GetAccessValue(a.Source)?.GetValue<object>();
            }).ToArray(), null, init.Value.Mode).ConfigureAwait(false);
            if (value is not null)
                TrySetValue(value);
        }
        catch(Exception ex)
        {
            ctx.LogError(ex, "Init data node of {0} failed with func {1}", Type.Name, init.Value.Func);
        }
    }

    #endregion

    #region Property Access

    /// <summary>
    /// Apply property effects
    /// </summary>
    public void ApplyPropertyEffects()
    {
        foreach (var p in GetProperties<IProperty>())
            p.Effect(this);
    }

    /// <summary>
    /// Join several property providers
    /// </summary>
    protected IEnumerable<T> JoinProperties<T>(params IEnumerable<T>[] providers) where T : IProperty
    {
        var types = new HashSet<Type>();
        foreach(var provider in providers)
        {
            foreach(var prop in provider)
            {
                if (prop.Stackable)
                    yield return prop;
                else
                {
                    if (types.Add(prop.GetType()))
                        yield return prop;
                }
            }
        }
    }

    /// <inheritdoc/>
    public T? GetProperty<T>() where T : class, IProperty
    {
        if (_props != null && _props.TryGetValue(typeof(T), out var props) && props.Count > 0)
            return props.First().Property as T;

        foreach (var provider in this._propertyProviders)
        {
            var prop = provider.GetProperty<T>();
            if (prop != null && prop.HasValue && prop.Inheritable) return prop;
        }
        return Type?.GetProperty<T>();
    }

    /// <inheritdoc/>
    public IEnumerable<T> GetProperties<T>() where T : IProperty
    {
        return JoinProperties(_FilterProperties<T>(), _propertyProviders.SelectMany(p => p.GetProperties<T>()).Where(p => p.Inheritable), Type?.GetProperties<T>().Where(p => p.Inheritable) ?? []);
    }

    /// <summary>
    /// Gets the property value type by property
    /// </summary>
    public T? GetPropertyValue<P, T>() where P : Property<T> => GetProperty<P>() is { } p ? p.Value : default(T?);

    /// <summary>
    /// Gets all the property values by type
    /// </summary>
    public IEnumerable<T> GetPropertyValues<P, T>() where P : Property<T>
    {
        foreach (var p in GetProperties<P>())
        {
            if (p.HasValue && p.Value != null)
                yield return p.Value;
        }
    }

    /// <summary>
    /// Gets the value type declared by the nearest <see cref="Property{T}"/> base of a property ctor.
    /// A property class may inherit through a non-generic intermediate class, for example
    /// EntrySource -> FuncCallProperty -> Property&lt;FuncCall&gt;, so the immediate base
    /// type must not be assumed to be generic.
    /// </summary>
    private static Type? GetPropertyValueType(Type propertyCtor)
    {
        for (Type? current = propertyCtor.BaseType; current != null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Property<>))
                return current.GetGenericArguments()[0];
        }
        return null;
    }

    /// <inheritdoc/>
    public void SetPropertyValue(Type propertyCtor, object? value, IValueAccess? source = null, IRelation? relation = null)
    {
        Type? valueType = GetPropertyValueType(propertyCtor);
        if (valueType == null) return;

        MethodInfo setter = _setters.GetOrAdd(propertyCtor, static type =>
        {
            Type? propValueType = GetPropertyValueType(type);
            return typeof(DataNode).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .First(m => m.Name == nameof(SetPropertyValue) && m.IsGenericMethod)
                .MakeGenericMethod(type, propValueType ?? typeof(object));
        });
        setter.Invoke(this, [value, source, relation]);
    }

    /// <inheritdoc/>
    public void SetPropertyValue<P, T>(T? value, IValueAccess? source = null, IRelation? relation = null) where P : Property<T>
    {
        source ??= this;
        var isClear = value is null;
        var props = _props?.GetValueOrDefault(typeof(P));
        var record = props?.FirstOrDefault(p => p.Source == source && (relation != null ? p.Relation == relation : p.Relation is null));
        var oldValue = record?.Property.GetValue<object>();
        IProperty? topProp = null;

        // clear
        if (isClear)
        {
            if (record == null) return;
            record.Property.SetValue(value);
            props!.Remove(record);
            _props![typeof(P)] = props;
        }
        // update
        else if(record is not null)
        {
            if (object.Equals(oldValue, value)) return;
            record.Property.SetValue(value);
            topProp = props!.First().Property;
        }
        // insert
        else
        {
            topProp = GetProperty<P>();

            // add new property
            _props ??= [];
            var prop = Activator.CreateInstance<P>();
            prop.Source = source;
            record = new PropertyRecord {
                Source = source,
                Relation = relation,
                Level = CalcLevel(source),
                Property = prop
            };
            record.Property.SetValue(value);
            if (props is not null)
            {
                props.Add(record);
                props.Sort((a, b) =>
                {
                    var diff = b.Level - a.Level;
                    if (diff != 0) return diff;
                    return (b.Relation != null ? 1 : 0) - (a.Relation != null ? 1 : 0);
                }); // keep order
            }
            else
            {
                props = [record];
            }
            _props[typeof(P)] = props;

            // replace the top property
            if (props.First().Equals(record))
            {
                if (FullMode && topProp != null && !topProp.Stackable) topProp.Clear(this);
                oldValue = topProp?.GetValue<object>();
                topProp = record.Property;
            }
        }

        // Validate constraint
        if (record.Property is IConstraintProperty constraint && Context is SchemaContext ctx)
        {
            if (isClear)
            {
                if (record.Valid == false) // clear violated
                    OnNextViolated();
            }
            else
            {
                // valid
                try
                {
                    var res = constraint.ValidateAsync(ctx, this).ConfigureAwait(false).GetAwaiter().GetResult();
                    if (res != record.Valid)
                    {
                        record.Valid = res;
                        OnNextViolated();
                    }
                }
                catch(Exception ex)
                {
                    ctx.LogError(ex, "Validation failed {0}", constraint.Name);
                }
            }
        }

        // Apply side effect
        if (record.Property.Stackable)
        {
            if (FullMode)
            {
                if (isClear)
                    record.Property.Clear(this);
                else
                    record.Property.Effect(this);
            }
            OnNextProperty(typeof(P), record.Property.GetValue<object>(), oldValue);
        }
        else if (isClear)
        {
            // if clear the top property, apply side effect
            if (props is not { Count: > 0} || props.First().Level < record.Level)
            {
                if (FullMode)
                    record.Property.Clear(this);
                topProp = GetProperty<P>();
                if (FullMode)
                    topProp?.Effect(this);
                OnNextProperty(typeof(P), topProp?.GetValue<object>(), oldValue);
            }
        }
        else if(topProp == record.Property)
        {
            if (FullMode)
                record.Property.Effect(this);
            OnNextProperty(typeof(P), record.Property.GetValue<object>(), oldValue);
        }
    }

    #endregion

    #region Subscription

    /// <summary>
    /// Publish the value change. Scalar nodes publish the raw value, composite nodes publish the node
    /// itself so subscribers (and semantic functions) can consume it without a JSON projection.
    /// </summary>
    protected void OnNext<T>(T? value)
    {
        if (!FullMode) return;
        _validated?.Cancel();
        _validated = new CancellationTokenSource();
        _dataOb?.OnNext((this, value));
        var token = _validated.Token;
        _ = Task.Run(async () => await ValidateAsync(token).ConfigureAwait(false));
    }

    /// <summary>
    /// Publish the violated status changed
    /// </summary>
    protected void OnNextViolated()
    {
        if (!FullMode) return;
        _violatedOb?.OnNext((this, IsValid));
    }

    protected void OnNextProperty(Type propertyCtor, object? value, object? oldValue)
    {
        if (!FullMode) return;
        (IValueAccess, Type, object?, object?) payload = (this, propertyCtor, value, oldValue);
        if (_selfPropObs != null && _selfPropObs.TryGetValue(propertyCtor, out var selfObs))
            selfObs.OnNext(payload);

        if (_propObs != null && _propObs.TryGetValue(propertyCtor, out var obs))
            obs.OnNext(payload);
    }

    /// <inheritdoc/>
    public virtual IDisposable Subscribe(IObserver<(IValueAccess, object?)> observer, bool immediate = false)
    {
        if (!FullMode) return _statciFakeSub;
        _dataOb ??= new Subject<(IValueAccess, object?)>();
        if (immediate) observer.OnNext((this, TryGetValue(out object? value) ? value : null));
        return _dataOb.Subscribe(observer);
    }

    /// <summary>
    /// Subscribe the data change with handler
    /// </summary>
    public IDisposable Subscribe(Action<(IValueAccess, object?)> handler, bool immediate = false)
        => Subscribe(Observer.Create(handler), immediate);

    /// <summary>
    /// Subscribe the property change for self property
    /// </summary>
    protected IDisposable SubscribeSelfProperty<P, T>(IObserver<(IValueAccess, Type, T?, T?)> observer, bool immediate = false) where P : Property<T>
    {
        if (!FullMode) return _statciFakeSub;
        _selfPropObs ??= new ConcurrentDictionary<Type, Subject<object>>();
        var obs = _selfPropObs.GetOrAdd(typeof(P), _ => new Subject<object>());
        if (immediate)
            observer.OnNext((this, typeof(P), GetPropertyValue<P, T>(), default(T?)));
        return obs.Subscribe(value => observer.OnNext(ToSelfPropertyChange<P, T>(value)));
    }

    /// <summary>
    /// Subscribe the property change with handler
    /// </summary>
    protected IDisposable SubscribeSelfProperty<P, T>(Action<(IValueAccess, Type, T?, T?)> handler, bool immediate = false) where P : Property<T>
        => SubscribeSelfProperty<P, T>(Observer.Create(handler), immediate);

    /// <inheritdoc/>
    public IDisposable SubscribeProperty<P, T>(IObserver<(IValueAccess, Type, T?, T?)> observer, bool immediate = false) where P : Property<T>
    {
        if (!FullMode) return _statciFakeSub;
        _propObs ??= new ConcurrentDictionary<Type, Subject<object>>();
        var obs = _propObs.GetOrAdd(typeof(P), _ => new Subject<object>());
        if (immediate)
            observer.OnNext((this, typeof(P), GetPropertyValue<P, T>(), default(T?)));
        return obs.Subscribe(value => observer.OnNext(ToSelfPropertyChange<P, T>(value)));
    }

    /// <summary>
    /// Subscribe the property change with handler
    /// </summary>
    public IDisposable SubscribeProperty<P, T>(Action<(IValueAccess, Type, T?, T?)> handler, bool immediate = false) where P : Property<T>
        => SubscribeProperty<P, T>(Observer.Create(handler), immediate);


    /// <summary>
    /// Subscribe the property from all ancestor
    /// </summary>
    public IDisposable SubscribeAncestorProperty<P, T>(IObserver<(IValueAccess, Type, T[])> observer, bool immediate = false) where P : Property<T>
    {
        List<IValueAccess> nodes = [];
        IValueAccess? node = this;
        while (node != null && !nodes.Contains(node))
        {
            nodes.Add(node);
            node = node.Parent;
        }
        nodes.Reverse();
                
        var multiSub = new MultiSub();
        var ob = Observer.Create<(IValueAccess, Type, T?, T?)>(args =>
        {
            T[] values = nodes.Where(n => n.GetProperty<P>()?.HasValue == true).Select(n => n.GetProperty<P>()!.Value!).ToArray();
            observer.OnNext((this, args.Item2, values));
        });
        foreach (var n in nodes) multiSub.Subscribe(n.SubscribeProperty<P, T>(ob));
        if (immediate) ob.OnNext((this, typeof(P), default(T?), default(T?)));

        return multiSub;
    }

    /// <summary>
    /// Subscribe the property from all ancestor with handler
    /// </summary>
    public IDisposable SubscribeAncestorProperty<P, T>(Action<(IValueAccess, Type, T[])> handler, bool immediate = false) where P : Property<T>
        => SubscribeAncestorProperty<P, T>(Observer.Create(handler), immediate);

    /// <summary>
    /// Convert the boxed property change payload to the typed property change
    /// </summary>
    private static (IValueAccess, Type, T?, T?) ToSelfPropertyChange<P, T>(object value) where P : Property<T>
    {
        (IValueAccess, Type, object?, object?) payload = ((IValueAccess, Type, object?, object?))value;
        return (payload.Item1, payload.Item2, payload.Item3 is T nv ? nv : default(T?), payload.Item4 is T ov ? ov : default(T?));
    }

    /// <inheritdoc/>
    public IDisposable SubscribeViolated(IObserver<(IValueAccess, bool)> observer, bool immediate = false)
    {
        if (!FullMode) return _statciFakeSub;
        _violatedOb ??= new Subject<(IValueAccess, bool)>();
        if (immediate) observer.OnNext((this, IsValid));
        return _violatedOb.Subscribe(observer);
    }

    /// <summary>
    /// Subscribe the data violated status with handler
    /// </summary>
    public IDisposable SubscribeViolated(Action<(IValueAccess, bool)> handler, bool immediate = false)
        => SubscribeViolated(Observer.Create(handler), immediate);

    /// <inheritdoc/>
    public virtual void RecordSubscription(IDisposable disposable, object? source)
    {
        source ??= this;
        _subs ??= new ConcurrentDictionary<object, ConcurrentBag<IDisposable>>();
        var bag = _subs.GetOrAdd(source, _ => new ConcurrentBag<IDisposable>());
        bag.Add(disposable);
    }

    /// <summary>
    /// Gets attached relations
    /// </summary>
    public IEnumerable<IRelation> GetAttachedRelations()
    {
        if (_subs is null) yield break;
        foreach(var source in _subs.Keys)
        {
            if (source is IRelation relation)
                yield return relation;
        }
    }

    /// <inheritdoc/>
    public virtual void ClearSubscription(object? source)
    {
        source ??= this;
        if (_subs != null && _subs.TryRemove(source, out var bag))
        {
            foreach (var d in bag)
                d.Dispose();
            bag.Clear();
        }
    }

    /// <summary>
    /// Subscribe the move subscription happened, when override type or other cause the data node full changed
    /// </summary>
    public IDisposable SubscribeMove(IObserver<IValueAccess> observer)
    {
        _moveObs ??= new Subject<IValueAccess>();
        return _moveObs.Subscribe(observer);
    }

    /// <summary>
    /// Move subscriptions to new node before destroying this node
    /// </summary>
    public void MoveSubscription(DataNode newNode)
    {
        newNode._dataOb = _dataOb;
        newNode._propObs = _propObs;
        newNode._violatedOb = _violatedOb;

        _dataOb = null;
        _propObs = null;
        _violatedOb = null;

        _moveObs?.OnNext(newNode);
    }

    #endregion

    #region Path Navigation

    /// <inheritdoc/>
    public IValueAccess? Parent { get; init; }

    /// <inheritdoc/>
    public string? Name => GetPropertyValue<Name, string>();

    /// <inheritdoc/>
    public string Access
    {
        get
        {
            IValueAccess? node = this;
            List<string> names = [];
            while (node is not null)
            {
                string? name = node.Name;
                if (string.IsNullOrWhiteSpace(name)) break;
                names.Insert(0, name);
                node = node.Parent;
            }
            return string.Join('.', names);
        }
    }

    /// <summary>
    /// Gets the access value by path
    /// </summary>
    public virtual IValueAccess? GetAccessValue(string path, IValueAccess? node = null)
    {
        if (string.IsNullOrEmpty(path)) return this;
        string[] parts = path.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        IEnumerable<IValueAccessPathHandler>? handlers = ((Type as Runtime.ValueType)?.Runtime as SchemaRuntime)?.GetRuntimeItem(typeof(IValueAccessPathHandler)) as IEnumerable<IValueAccessPathHandler>;
        if (handlers == null) return null;
        foreach (var handler in handlers)
        {
            if (handler.Path.Equals(parts[0], StringComparison.OrdinalIgnoreCase))
            {
                var type = handler.GetAccessValue(this, node);
                return parts.Length > 1 ? type?.GetAccessValue(parts[1]) : type;
            }
        }
        return null;
    }

    #endregion

    #region Relation

    /// <summary>
    /// Gets the relation that effect this node for the given property, works for non-full mode
    /// </summary>
    public (RelationType Relation, IValueAccess Owner)? GetRelation<T>() where T: IProperty
    {
        IValueAccess? curr = this.Parent;
        RelationType? last = null;
        IValueAccess? owner = null;
        while (curr != null)
        {
            foreach(RelationType r in curr!.Type.GetRelations())
            {
                if (r.Property?.GetCsharpType() != typeof(T)) continue;
                if (curr.GetAccessValue(r.Target, this) != this) continue;
                last = r;
                owner = curr;
                break;
            }
            curr = curr.Parent;
        }
        return last != null ? (last, owner!) : null;
    }

    /// <summary>
    /// Loading default value for node
    /// </summary>
    public async Task LoadDefaultAsync(SchemaContext context)
    {
        // For now only load default value for displayonly field
        if (!this.IsEmpty || this.GetProperty<DisplayOnly>()?.Value != true) return;
        var r = this.GetRelation<Default>();
        if (r is null) return;
        var d = await r.Value.Relation.CallAsync(context, r.Value.Owner, this).ConfigureAwait(false);
        if (d is null || !d.HasValue) return;
        this.TrySetValue(d.GetValue<object>());
    }

    /// <summary>
    /// Clear displayOnly node to avoid un-valid data
    /// </summary>
    public virtual void ClearDisplayOnlyNode() {
        if (this.GetProperty<DisplayOnly>()?.Value == true)
            this.ClearValue();
    }

    /// <inheritdoc/>
    public virtual void AttachRelations(IRelationInfo[] realtions)
    {
        // relations only work in full mode
        if (!FullMode) return;

        foreach (var info in realtions)
            foreach (var r in info.Relations)
                if (info.Owner.GetAccessValue(r.Target, this) == this)
                    r.Attach(info.Owner, this);
    }

    #endregion

    #region Validation

    /// <inheritdoc/>
    public IEnumerable<IConstraintProperty> GetViolatedConstraints()
    {
        var unStackable = new HashSet<Type>();
        if (_props is not null)
        {
            foreach (var props in _props.Values)
            {
                if (props.Count == 0 || props.First().Property is not IConstraintProperty) continue;

                foreach (var record in props)
                {
                    if (record.Valid is null) continue;
                    if (!record.Property.Stackable && !unStackable.Add(record.Property.GetType())) continue;
                    if (record.Valid != false) continue;
                    yield return (record.Property as IConstraintProperty)!;
                }
            }
        }

        if (_violated == null) yield break;
        foreach (var constraint in _violated)
        {
            if (!constraint.Stackable && !unStackable.Add(constraint.GetType())) continue;
            yield return constraint;
        }
    }

    /// <summary>
    /// Gets the error nodes
    /// </summary>
    public virtual IEnumerable<DataNode> GetErrorNodes()
    {
        if (!VisibleInTree) yield break;
        if (GetViolatedConstraints().Any())
            yield return this;
    }

    /// <summary>
    /// The error message
    /// </summary>
    public string? Error
    {
        get
        {
            var hasError = false;
            foreach(var p in GetViolatedConstraints())
            {
                var msg = p.GetErrorMessage(this);
                if (!string.IsNullOrWhiteSpace(msg)) return msg;
                hasError = true;
            }
            return hasError ? new LocaleString("VALUE_NOT_VALID").Format(Context, GetPropertyValue<Display, LocaleString>() ?? GetPropertyValue<Name, string>()) : null;
        }
    }

    /// <summary>
    /// Validate the data
    /// </summary>
    public virtual async Task ValidateAsync(CancellationToken cancellationToken)
    {
        if (DisableConstraint) return;
        if (Context is not SchemaContext ctx) return;

        // validate static constraints sequentially, constraints are few and lightweight
        var staticConstraints = _propertyProviders.SelectMany(p => p.GetProperties<IConstraintProperty>())
            .Concat(Type.GetProperties<IConstraintProperty>())
            .ToList();
        foreach (var p in staticConstraints)
        {
            if (cancellationToken.IsCancellationRequested) return;
            try
            {
                var res = await p.ValidateAsync(ctx, this, cancellationToken).ConfigureAwait(false);
                RecordConstraint(p, res, true);
            }
            catch (Exception ex)
            {
                ctx.LogError(ex, "Validation failed {0}", p.Name);
            }
        }

        // validate dynamic constraints sequentially
        if (_props is not null)
        {
            foreach (var props in _props.Values)
            {
                if (props.Count == 0 || props.First().Property is not IConstraintProperty) continue;
                foreach (var record in props)
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    try
                    {
                        var prop = (record.Property as IConstraintProperty)!;
                        var res = await prop.ValidateAsync(ctx, this, cancellationToken).ConfigureAwait(false);
                        if (res != record.Valid)
                            record.Valid = res;
                    }
                    catch (Exception ex)
                    {
                        ctx.LogError(ex, "Validation failed {0}", record.Property.Name);
                    }
                    if (!record.Property.Stackable) break; // only validate the top one for non-stackable
                }
            }
        }
        if (cancellationToken.IsCancellationRequested) return;
        OnNextViolated();
    }

    /// <summary>
    /// Whether the node is valid, which means no violated constraints
    /// </summary>
    public virtual bool IsValid => !VisibleInTree || DisableConstraint || !GetViolatedConstraints().Any();

    /// <inheritdoc/>
    public void RecordConstraint(IConstraintProperty constraint, bool? result)
    {
        RecordConstraint(constraint, result, false);
    }

    /// <inheritdoc/>
    public void RecordConstraint(IConstraintProperty constraint, bool? result, bool silent = false)
    {
        var map = _props?.GetValueOrDefault(constraint.GetType());
        var exist = map?.Find(r => r.Property == constraint);
        
        // dynamic
        if (exist != null)
        {
            if (exist.Valid == result) return;
            exist.Valid = result;
        }
        // passed
        else if(result == true || result == null)
        {
            if (_violated is null) return;
            var index = _violated.IndexOf(constraint);
            if (index < 0) return;
            _violated.RemoveAt(index);
        }
        // fail
        else
        {
            _violated ??= [];
            var index = _violated.IndexOf(constraint);
            if (index >= 0) return;
            _violated.Add(constraint);
        }
        if (!silent)
            OnNextViolated();
    }

    /// <summary>
    /// Clear constraints
    /// </summary>
    public void ClearConstraints()
    {
        _violated?.Clear();
        _validated?.Cancel();
        _validated = null;

        if (this._props is not null)
        {
            foreach (var props in this._props.Values)
            {
                if (props.Count == 0 || props.First().Property is not IConstraintProperty) continue;
                foreach (var record in props)
                {
                    if (record.Property is IConstraintProperty constraint)
                        record.Valid = null;
                }
            }
        }
        OnNextViolated();
    }

    /// <summary>
    /// Gets the violated
    /// </summary>
    public virtual JsonNode? Violated => _violated is {  Count: > 0 } ? new JsonArray(_violated.Select(v => JsonValue.Create(v.Name)).ToArray()) : null;

    #endregion

    #region Virtual

    /// <summary>
    /// The c# type representation
    /// </summary>
    public virtual Type? CsharpType => (Type as Runtime.ValueType)?.GetCsharpType();

    /// <summary>
    /// Equals check
    /// </summary>
    public virtual bool Equals(IValueAccess? other) 
        => other != null && 
           (ReferenceEquals(this, other) || 
            IsEmpty 
               ? other.IsEmpty 
               : TryGetValue(out object? thisValue) && 
                 other.TryGetValue(out object? otherValue) && 
                 Equals(thisValue, otherValue));

    /// <inheritdoc/>
    public override string? ToString() => GetValue<string>();

    #endregion

    #region Utility

    class PropertyRecord
    {
        /// <summary>
        /// The property source
        /// </summary>
        public required IValueAccess Source { get; set; }

        /// <summary>
        /// The source relation
        /// </summary>
        public IRelation? Relation { get; set; }

        /// <summary>
        /// The distant level of the source to target
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// The property
        /// </summary>
        public required IProperty Property { get; set; }

        /// <summary>
        /// Whether the target is valid for the property
        /// </summary>
        public bool? Valid { get; set; }
    }

    class FakeSub: IDisposable    {
        public void Dispose()
        {
        }
    }

    class MultiSub : IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new();

        public void Subscribe(IDisposable sub)
        {
            _subscriptions.Add(sub);
        }

        public void Dispose()
        {
            foreach (var subscription in _subscriptions)
                subscription.Dispose();

            _subscriptions.Clear();
        }
    }

    static FakeSub _statciFakeSub = new FakeSub();

    /// <summary>
    /// Creates the data node with the schema declared node type, falls back to the type default node when the node type is not specified
    /// </summary>
    public static IValueAccess CreateNode(ISchemaContext? context, IValueTypeAccess type, Type? nodeType, object? value = null, IValueAccess? parent = null, params IPropertyProvider[] propertyProviders)
    {
        if (nodeType is null || !typeof(IValueAccess).IsAssignableFrom(nodeType))
            return type.Create(context, value, parent, propertyProviders);

        ConstructorInfo ctor = _nodeCtors.GetOrAdd(nodeType, static t =>
            t.GetConstructor([typeof(ISchemaContext), typeof(IValueTypeAccess), typeof(object), typeof(IValueAccess), typeof(IPropertyProvider[])])
            ?? throw new InvalidOperationException($"The data node type {t.Name} has no valid constructor."));

        return (IValueAccess)ctor.Invoke([context, type, value, parent, propertyProviders]);
    }

    private int CalcLevel(IValueAccess source)
    {
        int level = 0;
        IValueAccess? curr = this;
        while (curr != null && curr != source)
        {
            level++;
            curr = curr.Parent;
        }
        return level;
    }

    /// <summary>
    /// Gets the override properties
    /// </summary>
    private IEnumerable<T> _FilterProperties<T>() where T : IProperty
    {
        if (_props is null) yield break;
        if (typeof(T).IsInterface)
        {
            foreach (var t in _props.Keys.Where(k => typeof(T).IsAssignableFrom(k)))
            {
                var s = _props[t];
                if (s.Count == 0) continue;
                var first = s.First().Property;
                if (first.HasValue && first is T v) yield return v;
                if (first.Stackable == true)
                {
                    for (int i = 1; i < s.Count; i++)
                    {
                        if (s[i].Property.HasValue && s[i].Property is T p)
                            yield return p;
                    }
                }
            }
        }
        else if (_props.TryGetValue(typeof(T), out var props))
        {
            if (props.Count == 0) yield break;
            var first = props.First().Property;
            if (first.HasValue && first is T v) yield return v;
            if (first.Stackable == true)
            {
                for (int i = 1; i < props.Count; i++)
                {
                    if (props[i].Property.HasValue && props[i].Property is T p)
                        yield return p;
                }
            }
        }
    }

    #endregion
}