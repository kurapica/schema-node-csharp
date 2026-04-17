using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Schema;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Runtime;

/// <summary>
/// The in-memory struct schema representation
/// </summary>
[Meta<AsErrorCode>("struct_wrong_field", SCHEMA_KIND_ORDER_STRUCT * 100 + 1)]
public sealed class StructType : AnySchemaType
{
    #region Data

    /// <summary>
    /// The struct fields
    /// </summary>
    public StructFieldSchema[] Fields { get; set; } = [];

    /// <summary>
    /// The union validations
    /// </summary>
    public StructUnionValidation[]? UnionValids { get; set; }

    #endregion

    #region Loading

    /// <inheritdoc />
    public override async Task LoadAsync(SchemaContext context, NodeSchema schema, bool preload = false)
    {
        StructSchema? @struct = schema.GetProperty<StructProperty>()?.Value;

        // Data
        Fields = @struct?.Fields ?? [];
        UnionValids = @struct?.UnionValids;

        // Status
        if (@struct == null) Error = "no_definition";

        // Load Fields — resolve each field's type reference
        ISchemaRuntime runtime = context.Runtime;
        foreach (StructFieldSchema field in Fields)
        {
            if (string.IsNullOrWhiteSpace(field.Type)) continue;

            AnySchemaType? fieldType = await runtime.GetSchemaTypeAsync(context, field.Type, preload: preload);
            if (fieldType != null)
            {
                fieldType.AddRef(this);
            }
            else
            {
                field.Error = $"Field type '{field.Type}' not found";
                Error = "struct_wrong_field";
            }
        }

        // Load union validation function refs
        if (UnionValids is { Length: > 0 })
        {
            foreach (StructUnionValidation valid in UnionValids)
            {
                if (string.IsNullOrWhiteSpace(valid.Func)) continue;
                AnySchemaType? funcNode = await runtime.GetSchemaTypeAsync(context, valid.Func, preload: preload);
                if (funcNode is FunctionType ft)
                {
                    valid.FuncNode = ft;
                    ft.AddRef(this);
                }
                else
                {
                    valid.Error = $"Function '{valid.Func}' not found";
                }
            }
        }
    }

    /// <summary>
    /// Gets the field by name
    /// </summary>
    public StructFieldSchema? GetField(string fieldName)
        => Fields.FirstOrDefault(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public override void ReleaseType()
    {
        if (UnionValids != null)
        {
            foreach (StructUnionValidation valid in UnionValids)
            {
                valid.FuncNode?.RemoveRef(this);
                valid.FuncNode = null;
            }
        }

        Fields = [];
        UnionValids = null;
        base.ReleaseType();
    }

    #endregion
}
