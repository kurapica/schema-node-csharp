using SchemaNode.Context;
using SchemaNode.Schema;
using SchemaNode.Utility;
using SchemaNode.Node;

namespace SchemaNode.Runtime;

/// <summary>
/// Abstract base for all scalar kind runtime types (bool, string, date, decimal, int, object).
/// </summary>
public abstract class ScalarType : ValueType
{
    #region Reference

    /// <summary>The base type node.</summary>
    public ScalarType? BaseNode { get; private set; }

    #endregion

    #region Implementations

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context)
    {
        BaseNode = null;
        ScalarSchema? scalar = GetPropertyValue<ScalarSchema>();

        if (!string.IsNullOrWhiteSpace(scalar?.Base))
        {
            BaseNode = await context.GetNodeTypeAsync<ScalarType>(scalar.Base);
            if (BaseNode == null || !BaseNode.Kind.Equals(Kind, StringComparison.OrdinalIgnoreCase))
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
    public override Type? GetCsharpType() => base.GetCsharpType() ?? BaseNode?.GetCsharpType();
    
    /// <inheritdoc />
    protected override Task ValidateNodeAsync(SchemaContext context, DataNode node)
        => BaseNode?.ValidateValueAsync(context, node) ?? Task.CompletedTask;

    #endregion
}

