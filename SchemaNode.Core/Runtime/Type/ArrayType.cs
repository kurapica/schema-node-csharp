using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Schema;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory array schema representation
/// </summary>
[Meta<ErrorCode>("array_wrong_element", SCHEMA_KIND_ORDER_ARRAY * 100 + 1)]
public sealed class ArrayType : AnySchemaType
{
    #region Data

    /// <summary>
    /// The element type name
    /// </summary>
    public string? Element { get; private set; }

    /// <summary>
    /// The primary fields of the array (if element is struct)
    /// </summary>
    public string[]? Primary { get; private set; }

    /// <summary>
    /// The data indexes
    /// </summary>
    public DataIndex[]? Indexes { get; private set; }

    /// <summary>
    /// The data combine rules
    /// </summary>
    public DataCombine[]? Combines { get; private set; }

    #endregion

    #region Ref

    /// <summary>
    /// The resolved element schema type
    /// </summary>
    public AnySchemaType? ElementSchemaType { get; internal set; }

    #endregion

    #region Loading

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        ArraySchema? array = schema.GetProperty<ArrayProperty>()?.Value;

        // Data
        Element = array?.Element;
        Primary = array?.Primary;
        Indexes = array?.Indexes;
        Combines = array?.Combines;

        // Status
        if (array == null) Error = "no_definition";

        // Resolve element type
        if (!string.IsNullOrWhiteSpace(Element))
        {
            AnySchemaType? elemType = await context.Runtime.GetSchemaTypeAsync(context, Element, preload: preload);
            if (elemType != null)
            {
                ElementSchemaType = elemType;
                elemType.AddRef(this);
            }
            else
            {
                ElementSchemaType = null;
                Error = "array_wrong_element";
            }
        }
    }

    /// <inheritdoc />
    public override void ReleaseType()
    {
        ElementSchemaType?.RemoveRef(this);
        ElementSchemaType = null;
        base.ReleaseType();
    }

    #endregion
}
