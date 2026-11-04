using SchemaNode.Node;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Enum;
using SchemaNode.Runtime;

namespace SchemaNode.WPF.View.NodeWrapper;

/// <summary>
/// Scalar-specific constraints. Read-only and disabled states are observed by the base wrapper
/// because struct/array views need the same enablement rules.
/// </summary>
public class ScalarWrapper : DataNodeWrapper
{
    private bool _require;
    private bool _asSuggest;
    private bool _leafOnly;

    /// <inheritdoc />
    public override string? DefaultTemplateKey => SchemaTemplateKeys.Input;

    public ScalarWrapper(DataNode node, SchemaViewRegistry? registry, string? fieldName, SchemaViewRole role)
        : base(node, registry, fieldName, role)
    {
        ScalarNodeImpl = node as ScalarNode
            ?? throw new ArgumentException($"Node '{node.Type.Name}' is not a scalar node.", nameof(node));

        Track(node.SubscribeProperty<Require, bool>(OnRequireChanged, immediate: true));
        Track(node.SubscribeProperty<AsSuggest, bool>(OnAsSuggestChanged, immediate: true));
        Track(node.SubscribeProperty<LeafOnly, bool>(OnLeafOnlyChanged, immediate: true));
    }

    /// <summary>The typed scalar node.</summary>
    protected ScalarNode ScalarNodeImpl { get; }

    /// <summary>Whether the field is required.</summary>
    public bool Require => _require;

    /// <summary>Whether the drop-down allows free text input.</summary>
    public bool AsSuggest => _asSuggest;

    /// <summary>Whether only leaf entries are selectable.</summary>
    public bool LeafOnly => _leafOnly;

    /// <summary>
    /// Whether the node is an enum array rendered as a multi-value popup. A plain scalar never is;
    /// the properties below exist on the base class because the input view binds all four selectors
    /// for every scalar-shaped wrapper. Without them the bindings fail and every editor would stay
    /// visible on top of the others.
    /// </summary>
    public virtual bool IsMulti => false;

    /// <summary>Whether the entry source has multiple cascaded levels.</summary>
    public virtual bool IsCascading => false;

    /// <summary>Whether the single-level drop-down is used.</summary>
    public virtual bool IsDropDown => false;

    /// <summary>Whether the plain text editor is used.</summary>
    public virtual bool IsPlainText => true;

    /// <summary>Hook invoked when the required constraint changes, for editors that branch on it.</summary>
    protected virtual void OnRequireChanged((IValueAccess node, Type propertyType, bool value, bool oldValue) args)
    {
        bool require = args.value;
        if (_require == require) return;
        _require = require;
        RaisePropertyChanged(nameof(Require));
    }

    private void OnAsSuggestChanged((IValueAccess node, Type propertyType, bool value, bool oldValue) args)
    {
        bool suggest = args.value;
        if (_asSuggest == suggest) return;
        _asSuggest = suggest;
        RaisePropertyChanged(nameof(AsSuggest));
    }

    private void OnLeafOnlyChanged((IValueAccess node, Type propertyType, bool value, bool oldValue) args)
    {
        bool leafOnly = args.value;
        if (_leafOnly == leafOnly) return;
        _leafOnly = leafOnly;
        RaisePropertyChanged(nameof(LeafOnly));
    }
}
