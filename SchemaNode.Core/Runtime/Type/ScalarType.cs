using SchemaNode.Context;
using SchemaNode.Schema;
using SchemaNode.Utility;
using SchemaNode.Node;
using SchemaNode.Property;

namespace SchemaNode.Runtime;

/// <summary>
/// Abstract base for all scalar kind runtime types (bool, string, date, decimal, int, object).
/// </summary>
public abstract class ScalarType : ValueType
{
    #region Properties

    /// <summary>The base type node.</summary>
    public ScalarType? BaseNode { get; private set; }

    #endregion
    
    #region Virtual

    /// <summary>
    /// Gets the scalar schema
    /// </summary>
    protected abstract ScalarSchema? GetScalarSchema();
    
    #endregion

    #region Implementations

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context)
    {
        BaseNode = null;
        ScalarSchema? scalar = GetScalarSchema();

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
    public override bool IsAssignableTo(IValueTypeAccess other)
        => Kind.Equals(other.Kind,  StringComparison.OrdinalIgnoreCase) || base.IsAssignableTo(other);

    /// <inheritdoc />
    public override Type? GetCsharpType() => base.GetCsharpType() ?? BaseNode?.GetCsharpType();
    
    #endregion

    #region Methods

    /// <summary>
    /// Gets the property with the given type
    /// </summary>
    public override T? GetProperty<T>() where T : class 
        => base.GetProperty<T>() ?? (BaseNode != null ? BaseNode.GetProperty<T>() : Runtime?.GetSchemaKindProperty<T>(Kind));

    /// <summary>
    /// Gets the properties with the given type
    /// </summary>
    public override IEnumerable<T> GetProperties<T>()
        => this.JoinProperties(base.GetProperties<T>(), BaseNode != null ? BaseNode.GetProperties<T>() : Runtime?.GetSchemaKindProperties<T>(Kind));
    
    #endregion
}

