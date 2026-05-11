using SchemaNode.Context;
using SchemaNode.Property;
using SchemaNode.Schema;
using SchemaNode.Utility;
using SchemaNode.Node;

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
        ScalarSchema? scalar = GetPropertyValue<ScalarSchema>();

        if (!string.IsNullOrWhiteSpace(scalar?.Base))
        {
            BaseNode = await context.GetNodeTypeAsync<ScalarType>(scalar.Base);
            if (BaseNode == null)
                Error = ErrorCodes.SCALAR_WRONG_BASE;
        }
    }

    /// <summary>
    /// Gets the reference types
    /// </summary>
    public override IEnumerable<NodeType> GetReferenceTypes()
    {
        if (BaseNode != null) yield return BaseNode;
        foreach(var nodeType in base.GetReferenceTypes())
            yield return nodeType;
    }

    /// <inheritdoc />
    public override bool IsAssignableTo(ValueType other)
        => Kind.Equals(other.Kind,  StringComparison.OrdinalIgnoreCase) || base.IsAssignableTo(other);

    /// <inheritdoc />
    public override async Task<DataNode> ValidateValueAsync(SchemaContext context, object? value)
    {
        DataNode result = ParseValue(value);
        if (value != null && result.IsEmpty)
        {
            result.Value = value;
            result.ViolatedConstraints = [Kind];
            return result;
        }
        
        List<string>? errors = null;
        foreach (IConstraintProperty constraint in Constraints)
        {
            if (await constraint.ValidateAsync(context, result) != false) continue;
            errors ??= [];
            errors.Add(constraint.Name);
        }
        if (errors != null)
            result.ViolatedConstraints = errors.ToArray();
        return result;
    }

    #endregion
}

