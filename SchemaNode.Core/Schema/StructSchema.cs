using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Record;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Scalar;
using SchemaNode.Service;
using SchemaNode.Utility;
using static SchemaNode.Utility.Constant;
using NodeType = SchemaNode.Property.Core.NodeType;

namespace SchemaNode.Schema;

/// <summary>
/// The struct schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_STRUCT, SCHEMA_KIND_ORDER_STRUCT)]
[Meta<NodeType>(typeof(StructType))]
[Meta<SchemaGenerator>(typeof(StructGenerator))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRUCT}.schema")]
[Meta<Attach>(SCHEMA_KIND_STRUCT)]
[Meta<Append>(typeof(Relations))]
[Relation<EntrySource>($"${nameof(UnionValids)}.{nameof(StructUnionValidation.Args)}.{nameof(CallArg.Source)}", NS_SYSTEM_SCHEMA_REFLECT_GET_SUB_ENTRIES, RELATION_OWNER, NODE_SELF)]
public sealed class StructSchema : ExtensibleSchema
{
    /// <summary>
    /// The struct fields
    /// </summary>
    public StructFieldSchema[] Fields { get; set; } = [];
    
    /// <summary>
    /// The union validations
    /// </summary>
    public StructUnionValidation[]? UnionValids { get; set; }

    /// <inheritdoc/>
    public override void CombineExtensions(ExtensibleSchema? other, ISchemaRuntime? runtime = null)
    {
        if (other is not StructSchema otherStruct) return; 
        base.CombineExtensions(otherStruct, runtime);

        // Find the one that contains another
        if (Fields.All(f => otherStruct.Fields.Any(of => f.Name.Equals(of.Name, StringComparison.OrdinalIgnoreCase))))
        {
            foreach (StructFieldSchema field in otherStruct.Fields)
            {
                StructFieldSchema? match = Fields.FirstOrDefault(x => x.Name == field.Name);
                if (match is not null)
                    field.CombineExtensions(match, runtime);
            }
            Fields = otherStruct.Fields;
        }
        else
        {
            foreach (StructFieldSchema field in Fields)
            {
                StructFieldSchema? match = otherStruct.Fields.FirstOrDefault(x => x.Name == field.Name);
                if (match is not null)
                    field.CombineExtensions(match, runtime);
            }
        }
        
        // Combine the union valids
        if (otherStruct.UnionValids is { Length: > 0 })
        {
            if (UnionValids is null || UnionValids.Length == 0)
            {
                UnionValids = otherStruct.UnionValids[..];
            }
            else
            {
                List<StructUnionValidation> combined = new List<StructUnionValidation>(UnionValids);
                foreach (StructUnionValidation union in otherStruct.UnionValids)
                {
                    if (union.Args.All(a => !a.Value.IsEmpty() || 
                                            a.Source?.Split('.').FirstOrDefault() is { } source &&
                                            Fields.Any(f => f.Name.Equals(source, StringComparison.OrdinalIgnoreCase)))
                            && combined.All(f => !f.Equals(union)))
                        combined.Add(union);
                }
                UnionValids = combined.ToArray();
            }
        }
    }
}

/// <summary>
/// Declare struct property for node schema
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.struct")]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_STRUCT)]
public sealed class StructProperty: Property<StructSchema>;

/// <summary>
/// Represents the struct type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRUCT}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, SCHEMA_KIND_STRUCT)]
public class StructType: ValueType;

/// <summary>
/// The struct field schema
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRUCT}.field")]
[Meta<SchemaKind>(SCHEMA_KIND_STRUCT_FIELD, SCHEMA_KIND_ORDER_STRUCT_FIELD)]
[Meta<Attach>(SCHEMA_KIND_STRUCT_FIELD)]
public sealed class StructFieldSchema : ExtensibleSchema
{
    /// <summary>
    /// The field name
    /// </summary>
    [Meta<SchemaType>(typeof(Identifier))]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The type name of the node.
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string Type { get; set; } = string.Empty;

    /// <inheritdoc/>
    public override bool Equals(ExtensibleSchema? other)
    {
        if (other is not StructFieldSchema otherField) return false;
        return ReferenceEquals(this, otherField) || Name.Equals(otherField.Name, StringComparison.OrdinalIgnoreCase);
    }
}

[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_STRUCT}.unionvalid")]
public class StructUnionValidation: IEquatable<StructUnionValidation>
{
    /// <summary>
    /// The union validation func
    /// </summary>
    [Meta<SchemaType>(typeof(ValidFuncType))]
    public string Func { get; set; } = string.Empty;

    /// <summary>
    /// The func arguments
    /// </summary>
    public CallArg[] Args { get; set; } = [];

    /// <summary>
    /// The error message
    /// </summary>
    [SchemaIgnore]
    public string? Error { get; set; }

    /// <summary>
    /// The function node ref
    /// </summary>
    [JsonIgnore]
    [SchemaIgnore]
    public FunctionType? FuncNode { get; set; }
    
    /// <summary>
    /// Whether the schema is equals
    /// </summary>
    public bool Equals(StructUnionValidation? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Func == other.Func && Args.SequenceEqual(other.Args);
    }
}