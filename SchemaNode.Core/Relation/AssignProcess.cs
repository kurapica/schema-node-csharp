using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Function;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Relation;

/// <summary>
/// The relation assign
/// </summary>
public class AssignProcess : IRelationProcess
{
    // The assign property
    private IProperty? _value;

    /// <inheritdoc/> 
    public async Task LoadAsync(SchemaContext context, RelationSchema schema, IValueTypeAccess owner)
    {
        var value = schema.GetProperty<Assign>()?.GetValue<object>();
        var propType = (await context.GetNodeTypeAsync(schema.Property))?.GetCsharpType();
        if (propType != null && propType.IsAssignableTo(typeof(IProperty)))
        {
            _value = Activator.CreateInstance(propType) as IProperty;
            _value?.SetValue(value);
        }
    }

    /// <inheritdoc/> 
    public Task<object?> ProcessAsync(SchemaContext context, IValueAccess owner, IValueAccess? target = null) 
        => Task.FromResult(_value?.GetValue<object>());
}

/// <summary>
/// Declare relation call field for the relation
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_RELATION)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_RELATION}.assign")]
[Meta<Property.Record.RelationKind>("assign", 0)]
[Meta<RelationProcess>(typeof(AssignProcess))]
[Relation<Visible, Call>(nameof(Assign), NS_SYSTEM_LOGIC_EQ, $"@{nameof(RelationSchema.Kind)}", "assign")]
[Relation<OverrideType, Call>(nameof(Assign), $"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(Function.Reflect.Type.getproptype)}", $"@{nameof(RelationSchema.Property)}")]
public class Assign : Property<object>;
