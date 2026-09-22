using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Function;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Runtime;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Relation;

/// <summary>
/// The relation any check, skip in backend now
/// </summary>
public class AnyProcess : IRelationProcess, INodeReferences, IErrorProvider
{
    public static RelationSchema[] CombineAnyRelations(IEnumerable<RelationSchema> schemas)
    {
        List<RelationSchema> relations = [];

        foreach (RelationSchema schema in schemas)
        {
            var exist = schema.Kind == "call" ? relations.FirstOrDefault(r => r.Target.Equals(schema.Target, StringComparison.OrdinalIgnoreCase) &&
                r.Property.Equals(schema.Property, StringComparison.OrdinalIgnoreCase)) : null;
            if (exist != null)
            {
                if (exist.Kind == "any")
                {
                    var any = exist.GetProperty<Any>()!;
                    any.SetValue(any.GetValue<FuncCall[]>()!.Concat([schema.GetProperty<Call>()!.Value]));
                    exist.SetProperty(any);
                    continue;
                }
                else if(exist.Kind == "call")
                {
                    var replace = new RelationSchema { Kind = "any", Property = exist.Property, Target = exist.Target };
                    var any = new Any();
                    any.SetValue<FuncCall[]>([schema.GetProperty<Call>()!.Value!, exist.GetProperty<Call>()!.Value!]);
                    replace.SetProperty(any);
                    relations.Add(replace);
                    continue;
                }
            }
            relations.Add(schema);
        }
        return relations.ToArray();
    }

    /// <summary>
    /// The load error
    /// </summary>
    public string? Error { get; private set; }


    /// <inheritdoc/>
    public async Task LoadAsync(SchemaContext context, RelationSchema schema, IValueTypeAccess owner)
    {
    }

    /// <inheritdoc/>
    public async Task<object?> ProcessAsync(SchemaContext context, IValueAccess owner, IValueAccess? target = null)
    {
        return null;
    }

    /// <inheritdoc/>
    public IEnumerable<Runtime.NodeType> GetReferenceTypes()
    {
        yield break;
    }
}

/// <summary>
/// Declare relation call field for the relation
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_RELATION)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROP_RELATION}.any")]
[Meta<Property.Record.RelationKind>("any", 1)]
[Meta<RelationProcess>(typeof(AnyProcess))]
[Relation<Visible, Call>(nameof(Call), NS_SYSTEM_LOGIC_EQ, $"@{nameof(RelationSchema.Kind)}", "any")]
[Relation<Default, Call>($"{nameof(Any)}.{ARRAY_ELEMENT}.{nameof(FuncCall.Return)}", $"{NS_SYSTEM_INTRINSIC}.{nameof(SystemIntrinsic.assign)}", NS_SYSTEM_BOOL)]
public class Any : Property<FuncCall[]>;