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
    public ScalarType? BaseType { get; private set; }

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
        BaseType = null;
        ScalarSchema? scalar = GetScalarSchema();

        if (!string.IsNullOrWhiteSpace(scalar?.Base))
        {
            BaseType = await context.GetNodeTypeAsync<ScalarType>(scalar.Base);
            if (BaseType == null || !BaseType.Kind.Equals(Kind, StringComparison.OrdinalIgnoreCase))
                Error = ErrorCodes.SCALAR_WRONG_BASE;
        }
    }

    /// <summary>
    /// Gets the reference types
    /// </summary>
    public override IEnumerable<NodeType> GetReferenceTypes()
    {
        if (BaseType != null) yield return BaseType;
        foreach(var nodeType in base.GetReferenceTypes())
            yield return nodeType;
    }

    /// <inheritdoc />
    public override bool IsAssignableTo(IValueTypeAccess other)
        => Kind.Equals(other.Kind,  StringComparison.OrdinalIgnoreCase) || base.IsAssignableTo(other);

    /// <inheritdoc />
    public override Type? GetCsharpType() => base.GetCsharpType() ?? BaseType?.GetCsharpType();
    
    #endregion

    #region Methods

    /// <summary>
    /// Gets the property with the given type
    /// </summary>
    public override T? GetProperty<T>() where T : class 
        => base.GetProperty<T>() ?? (BaseType != null ? BaseType.GetProperty<T>() : Runtime?.GetSchemaKindProperty<T>(SchemaKind));

    /// <summary>
    /// Gets the properties with the given type
    /// </summary>
    public override IEnumerable<T> GetProperties<T>()
        => this.JoinProperties(base.GetProperties<T>(), BaseType != null ? BaseType.GetProperties<T>() : Runtime?.GetSchemaKindProperties<T>(SchemaKind));
    
    #endregion
}

