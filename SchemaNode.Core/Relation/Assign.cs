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
public class Assign : IRelationProcess
{
    /// <summary>
    /// The value assign to property
    /// </summary>
    public object? Value { get; private set; }

    /// <inheritdoc/> 
    public Task LoadAsync(SchemaContext context, RelationSchema schema, IValueTypeAccess owner)
    {
        Value = schema.GetProperty<AssignProperty>()?.GetValue<object>();
        return Task.CompletedTask;
    }

    /// <inheritdoc/> 
    public Task<object?> ProcessAsync(SchemaContext context, IValueAccess owner) => Task.FromResult(Value);
}

/// <summary>
/// Declare relation call field for the relation
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_RELATION)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_RELATION}.assign")]
[Meta<Property.Record.RelationKind>("assign", 0)]
[Meta<RelationProcess>(typeof(Assign))]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(RelationSchema.Kind)}", "assign")]
[Relation<OverrideType>($"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemReflect.getproptype)}", $"${nameof(RelationSchema.Property)}")]
public class AssignProperty : Property<object>;
