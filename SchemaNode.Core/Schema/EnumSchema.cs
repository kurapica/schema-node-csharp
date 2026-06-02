using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property;
using SchemaNode.Property.Common;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using SchemaNode.Service;
using SchemaNode.Struct;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.Constant;
using NodeSchemaKind = SchemaNode.Property.Record.NodeSchemaKind;
using ValueSchemaKind = SchemaNode.Property.Record.ValueSchemaKind;
using SchemaKind =  SchemaNode.Property.Record.SchemaKind;
using NodeType = SchemaNode.Property.Core.NodeType;
using SchemaType = SchemaNode.Property.Core.SchemaType;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The enum schema
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_ENUM, SCHEMA_KIND_ORDER_ENUM)]
[Meta<NodeSchemaKind>(SCHEMA_KIND_ENUM, SCHEMA_KIND_ORDER_ENUM)]
[Meta<ValueSchemaKind>(SCHEMA_KIND_ENUM, SCHEMA_KIND_ORDER_ENUM)]
[Meta<NodeType>(typeof(EnumType))]
[Meta<SchemaGenerator>(typeof(EnumGenerator))]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ENUM}.schema")]
public sealed class EnumSchema : ExtensibleSchema
{
    /// <summary>
    /// The enum value type
    /// </summary>
    public EnumValueType Type { get; set; }
    
    /// <summary>
    /// The cascades of the enum value
    /// </summary>
    public LocaleString[]? Cascade { get; set; }

    /// <summary>
    /// The enum values
    /// </summary>
    public EnumValueSchema[] Values { get; set; } = [];
}

/// <summary>
/// Declare enum property for node schema
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_CORE}.enum")]
[Relation<Visible>(NS_SYSTEM_LOGIC_EQ, $"${nameof(NodeSchema.Kind)}", SCHEMA_KIND_ENUM)]
public sealed class EnumProperty: Property<EnumSchema>;

/// <summary>
/// Represents the enum type
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ENUM}.type")]
[Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_IS_SCHEMA_KIND, NODE_SELF, SCHEMA_KIND_ENUM)]
public class EnumType: ValueType;

/// <summary>
/// The enum value info
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_ENUM_VALUE, SCHEMA_KIND_ORDER_ENUM_VALUE)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_ENUM}.value")]                                                    
public sealed class EnumValueSchema: ExtensibleSchema
{
    /// <summary>
    /// The value
    /// </summary>
    [Meta<PrimaryIndex>]
    [Meta<UniqueIndex>("SUB_LIST", 1)]
    [Meta<UplimitString>(PRIMARY_KEY_MAX_LEN)]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// The root value
    /// </summary>
    [Meta<UniqueIndex>("SUB_LIST", 0)]
    [Meta<UplimitString>(PRIMARY_KEY_MAX_LEN)]
    public string? Root { get; set; }
    
    #region Runtime info
    
    /// <summary>
    /// Whether the enum value has sub enum values
    /// </summary>
    [SchemaIgnore]
    public bool? HasSubList { get; set; }
    
    /// <summary>
    /// The sub enum values
    /// </summary>
    [SchemaIgnore]
    public EnumValueSchema[]? SubList { get; set; }

    /// <summary>
    /// Whether the enum value is fully loaded
    /// </summary>
    [JsonIgnore]
    [SchemaIgnore]
    internal bool IsFullyLoaded { get; set; }

    /// <summary>
    /// The parent of the enum value
    /// </summary>
    [JsonIgnore]
    [SchemaIgnore]
    internal EnumValueSchema? Parent { get; set; }

    /// <summary>
    /// The cascade level
    /// </summary>
    [JsonIgnore]
    [SchemaIgnore]
    internal int Level { get; set;  }
    
    #endregion
    
    /// <summary>
    /// Clones the enum value with limit level
    /// </summary>
    /// <param name="limitLevel"></param>
    /// <returns></returns>
    internal EnumValueSchema Clone(int limitLevel = 0)
    {
        var schema = new EnumValueSchema
        {
            Value = Value,
            HasSubList = HasSubList,
            SubList = (HasSubList ?? false) && SubList is { Length: > 0 } && limitLevel > 0 
                ? SubList.Select(e => e.Clone(limitLevel - 1)).ToArray()
                : null
        };
        schema.CombineExtensions(this);
        return schema;
    }
    
    /// <summary>
    /// Combine the access list
    /// </summary>
    /// <param name="accesses"></param>
    internal void CombineAccessList(EnumValueAccess[] accesses)
    {
        if (accesses.Length == 0) return;
        EnumValueAccess current = accesses[0];

        if (current.SubList is not null)
        {
            // replace with new
            if (SubList is not null && SubList.Length > 0) {
                foreach (var v in current.SubList)
                {
                    EnumValueSchema? match = SubList!.FirstOrDefault(x => x.Value.Equals(v.Value, StringComparison.OrdinalIgnoreCase));
                    if (match is not null) v.SubList = match.SubList;
                }
            }

            SubList = current.SubList;

            if (accesses.Length > 1)
            {
                EnumValueSchema? match = SubList!.FirstOrDefault(x => x.Value.Equals(current.Value, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                    match.CombineAccessList(accesses.Skip(1).ToArray());
            }
        }
    }
}

/// <summary>
/// The enum value access info
/// </summary>
public sealed class EnumValueAccess
{
    /// <summary>
    /// The cascade name
    /// </summary>
    public LocaleString? Name { get; set; }
    
    /// <summary>
    /// The enum value of the cascade
    /// </summary>
    public string Value { get; set; } = string.Empty;
    
    /// <summary>
    /// The sublist of the enum value
    /// </summary>
    public EnumValueSchema[]? SubList { get; set; }
}