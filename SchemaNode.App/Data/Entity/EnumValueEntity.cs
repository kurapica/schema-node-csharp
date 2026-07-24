using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Property.App;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using SchemaNode.Struct;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;


namespace SchemaNode.Data.Entity;

[Meta<App>(NS_SYSTEM_SCHEMA)]
[Meta<EnableStorage>(true)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.entity.enumvalue")]
public class EnumValueEntity
{
    [Meta<PrimaryIndex>(0)]
    [Meta<UniqueIndex>("SUB_LIST", 0)]
    public string Enum { get; set; } = string.Empty;
    
    /// <summary>
    /// The value
    /// </summary>
    [Meta<PrimaryIndex>(1)]
    [Meta<UniqueIndex>("SUB_LIST", 2)]
    [Meta<UpLimitString>(PRIMARY_KEY_MAX_LEN)]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// The root value
    /// </summary>
    [Meta<UniqueIndex>("SUB_LIST", 1)]
    [Meta<UpLimitString>(PRIMARY_KEY_MAX_LEN)]
    public string? Root { get; set; }
    
    /// <summary>
    /// The seqno
    /// </summary>
    public long Seqno { get; set; }
    
    /// <summary>
    /// Has sub enum value list
    /// </summary>
    public bool HasChildren { get; set; }
    
    /// <summary>
    /// The extension properties of the node
    /// </summary>
    public JsonObject? Extensions { get; set; }

    #region Conversion

    public static implicit operator EnumValueEntity?(Entry<string>? enumValueSchema)
    {
        if  (enumValueSchema == null) return null;
        return new EnumValueEntity
        {
            Value = enumValueSchema.Value,
            HasChildren = enumValueSchema.HasChildren ?? false,
            Extensions = enumValueSchema.Extensions?.DeepClone() as JsonObject
        };
    }
    
    public static implicit operator Entry<string>?(EnumValueEntity? enumValueEntity)
    {
        if (enumValueEntity == null) return null;
        return new Entry<string>
        {
            Value = enumValueEntity.Value,
            HasChildren = enumValueEntity.HasChildren,
            Extensions = enumValueEntity.Extensions?.DeepClone() as JsonObject
        };
    }

    #endregion
}