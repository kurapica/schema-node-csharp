using System.Text.Json.Nodes;
using SchemaNode.Attribute;
using SchemaNode.Property.App;
using SchemaNode.Property.Constraint;
using SchemaNode.Property.Core;
using SchemaNode.Schema;
using SchemaNode.Utility;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;


namespace SchemaNode.Data.Entity;

[Meta<App>($"{NS_SYSTEM_SCHEMA}")]
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
    [Meta<UniqueIndex>("SUB_LIST", 3)]
    [Meta<UplimitString>(PRIMARY_KEY_MAX_LEN)]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// The root value
    /// </summary>
    [Meta<UniqueIndex>("SUB_LIST", 1)]
    [Meta<UplimitString>(PRIMARY_KEY_MAX_LEN)]
    public string? Root { get; set; }
    
    /// <summary>
    /// The seqno
    /// </summary>
    [Meta<UniqueIndex>("SUB_LIST", 2)]
    public long Seqno { get; set; }
    
    /// <summary>
    /// Has sub enum value list
    /// </summary>
    public bool HasSubList { get; set; }
    
    /// <summary>
    /// The extension properties of the node
    /// </summary>
    public JsonObject? Extensions { get; set; }

    #region Conversion

    public static implicit operator EnumValueEntity?(EnumValueSchema? enumValueSchema)
    {
        if  (enumValueSchema == null) return null;
        JsonObject? extensions = null;
        if (enumValueSchema.Extensions is { Count : > 0})
        {
            extensions = new JsonObject();
            foreach (var kvp in enumValueSchema.Extensions)
            {
                extensions[kvp.Key] = kvp.Value.DeepClone();
            }
        }
        
        return new EnumValueEntity
        {
            Enum = enumValueSchema.Parent?.Root ?? enumValueSchema.Value,
            Value = enumValueSchema.Value,
            Root = enumValueSchema.Root,
            Seqno = enumValueSchema.Seqno,
            HasSubList = enumValueSchema.HasSubList ?? false,
            Extensions = extensions
        };
    }
    
    public static implicit operator EnumValueSchema?(EnumValueEntity? enumValueEntity)
    {
        if (enumValueEntity == null) return null;
        Dictionary<string, JsonNode>? extensions = null;
        if (enumValueEntity.Extensions is { Count : > 0})
        {
            extensions = [];
            foreach (var kvp in enumValueEntity.Extensions)
            {
                if (kvp.Value != null && !kvp.Value.IsEmpty()) 
                    extensions[kvp.Key] = kvp.Value.DeepClone();
            }
        }
        
        return new EnumValueSchema
        {
            Value = enumValueEntity.Value,
            Root = enumValueEntity.Root,
            Seqno = enumValueEntity.Seqno,
            HasSubList = enumValueEntity.HasSubList,
            Extensions = extensions
        };
    }

    #endregion
}