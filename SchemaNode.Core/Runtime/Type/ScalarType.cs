using SchemaNode.Context;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Utility;
using SchemaNode.Attribute;
using SchemaNode.Node;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// Abstract base for all scalar kind runtime types (bool, string, date, decimal, int, object).
/// </summary>
public abstract class ScalarType : ValueType
{
    #region Ref

    /// <summary>The base type node.</summary>
    public ScalarType? BaseNode { get; private set; }

    #endregion

    #region Methods

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context)
    {
        BaseNode = null;
        ScalarSchema? schema = GetPropertyValue<ScalarSchema>();

        if (!string.IsNullOrWhiteSpace(schema?.Base))
        {
            BaseNode = await context.GetNodeTypeAsync<ScalarType>(schema.Base);
            if (BaseNode == null)
                Error = ErrorCodes.SCALAR_WRONG_BASE;
        }
    }

    /// <inheritdoc />
    public override void Release() => BaseNode?.RemoveRef(this);
    
    /// <inheritdoc />
    public override IEnumerable<NodeType> GetDependNodes()
    {
        if (BaseNode != null) yield return BaseNode;
    }

    /// <inheritdoc />
    public override bool IsAssignableTo(ValueType other)
    {
        if (base.IsAssignableTo(other)) return true;
        if (!Kind.Equals(other.Kind)) return false;
        return BaseNode != null && BaseNode.IsAssignableTo(other);
    }

    /// <inheritdoc />
    public override async Task<DataNode?> ValidateValueAsync(SchemaContext context, object value)
    {
        DataNode? result = ParseValue(value);
        if (result == null) return null;

        List<string>? errors = null;
        foreach (IConstraintProperty constraint in Constraints)
        {
            if (await constraint.ValidateAsync(context, result) != false) continue;
            errors ??= [];
            errors.Add(constraint.Type.GetPropertyName());
        }
        if (errors != null)
            result.ViolatedConstraints = errors.ToArray();
        return result;
    }

    /// <summary>
    /// Parse value to data node
    /// </summary>
    public abstract DataNode? ParseValue(object value);

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

