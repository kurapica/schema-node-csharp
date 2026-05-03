using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Node;
using SchemaNode.Property;
using SchemaNode.Property.Constraint;
using SchemaNode.Schema;
using SchemaNode.Utility;
using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Property.Schema;
using static SchemaNode.Utility.Extension;
using static SchemaNode.Utility.Constant;
using JsonNode = System.Text.Json.Nodes.JsonNode;

namespace SchemaNode.Runtime;

/// <summary>
/// Abstract base for all scalar kind runtime types (bool, string, date, decimal, int, object).
/// </summary>
public abstract class ScalarType : ValueType
{
    #region Data

    /// <summary>The base type name.</summary>
    public string? Base { get; private set; }

    /// <summary>The resolved up-limit value (from constraint properties).</summary>
    public object? UpLimit
    {
        get
        {
            if (GetProperty<UplimitString>() is { HasValue: true } us) return us.Value;
            if (GetProperty<UplimitNumber>() is { HasValue: true } un) return un.Value;
            if (GetProperty<UplimitDate>()   is { HasValue: true } ud) return ud.Value;
            return null;
        }
    }

    /// <summary>The resolved low-limit value (from constraint properties).</summary>
    public object? LowLimit
    {
        get
        {
            if (GetProperty<LowLimitString>() is { HasValue: true } ls) return ls.Value;
            if (GetProperty<LowLimitNumber>() is { HasValue: true } ln) return ln.Value;
            if (GetProperty<LowLimitDate>()   is { HasValue: true } ld) return ld.Value;
            return null;
        }
    }

    #endregion

    #region Ref

    /// <summary>The base scalar type node.</summary>
    public ScalarType? BaseNode { get; private set; }

    #endregion

    #region Abstract

    /// <summary>
    /// Returns the base schema name from the kind-specific property on the schema.
    /// Return null if this kind has no base hierarchy.
    /// </summary>
    protected abstract string? GetSchemaBase(NodeSchema schema);

    /// <summary>
    /// Validates and converts the supplied JSON value to a <see cref="DataNode"/>.
    /// </summary>
    public abstract Task<(Node.DataNode? value, JsonNode? error)> ValidateValueAsync(
        SchemaContext context, JsonNode value, IReadOnlyList<IConstraintProperty>? constraints = null);

    #endregion

    #region Methods

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema)
    {
        Base = GetSchemaBase(schema);

        if (!string.IsNullOrWhiteSpace(Base))
        {
            ScalarType? node = await context.GetNodeTypeAsync<ScalarType>(Base);
            if (node != null) { BaseNode = node; node.AddRef(this); }
            else { BaseNode = null; ErrorCode = ErrorCodes.SCALAR_WRONG_BASE; }
        }

        ValueType = schema.Type?.GetMetaProperty<ScalarValue>()?.Value
            ?? BaseNode?.ValueType ?? ScalarValueType.None;
    }

    /// <summary>Gets the up-limit converted to the requested type.</summary>
    public T? GetUpLimit<T>() where T : struct
    {
        if (UpLimit == null) return null;
        object? uplimit = Utility.Extension.TryConvert(typeof(T), UpLimit);
        if (uplimit == null) return null;
        return (T)uplimit;
    }

    /// <summary>Gets the low-limit converted to the requested type.</summary>
    public T? GetLowlimit<T>()
    {
        if (LowLimit == null) return default;
        object? lowlimit = Utility.Extension.TryConvert(typeof(T), LowLimit);
        if (lowlimit == null) return default;
        return (T)lowlimit;
    }

    /// <inheritdoc />
    public override void Release() => BaseNode?.RemoveRef(this);

    /// <summary>Whether this scalar type can act as <paramref name="other"/>.</summary>
    public virtual bool CanBeUseAs(NodeType other, bool exactly = false) =>
        this == other
        || Name.Equals(other.Name)
        || Name.Equals(NS_SYSTEM_OBJECT)
        || other.Name.Equals(NS_SYSTEM_OBJECT)
        || other switch
        {
            ScalarType scalar =>
                scalar.IsString ||
                (scalar.IsInt
                    ? IsInt
                    : (scalar.IsNumber
                        ? IsNumber
                        : (scalar.ValueType & ValueType) > 0)),
            EnumType @enum => @enum.ValueType switch
            {
                EnumValueType.String => IsString,
                EnumValueType.Int    => IsInt,
                EnumValueType.Flags  => IsInt,
                _                    => false
            },
            _ => false
        };

    /// <inheritdoc />
    public override bool IsIndexable =>
        (ValueType & ScalarValueType.Indexable) > 0 ||
        (ValueType & ScalarValueType.String) > 0 && UpLimit is <= ENTITY_PRIMARY_KEY_MAX_LEN;

    /// <inheritdoc />
    public override IEnumerable<NodeType> GetDependNodes()
    {
        if (BaseNode != null) yield return BaseNode;
    }

    /// <summary>
    /// Runs all loaded constraints (with optional per-call overrides) against <paramref name="result"/>.
    /// Returns false if any constraint fails.
    /// </summary>
    protected async Task<bool> ApplyConstraints(
        SchemaContext context, ScalarNode result, IReadOnlyList<IConstraintProperty>? overrides)
    {
        foreach (IConstraintProperty constraint in Constraints)
        {
            IConstraintProperty active =
                overrides?.FirstOrDefault(c => c.GetType() == constraint.GetType()) is { HasValue: true } ov
                    ? ov : constraint;
            if (await active.ValidateScalarAsync(context, result) == false)
                return false;
        }
        return true;
    }

    /// <summary>Parses a string to a bool (accepts "true"/"false"/0/1).</summary>
    protected static bool TryParseBoolValue(string value, out bool ret)
    {
        ret = false;
        if (string.IsNullOrEmpty(value)) return false;
        value = value.ToLower();
        switch (value)
        {
            case "true":  ret = true;  return true;
            case "false": ret = false; return true;
            default:
                if (!int.TryParse(value, out int val) || val is < 0 or > 1) return false;
                ret = val == 1;
                return true;
        }
    }

    #endregion
}

