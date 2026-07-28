using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property.App;
using SchemaNode.Property.Core;
using SchemaNode.Scalar;
using SchemaNode.Schema;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Data.Entity;

[Meta<App>(NS_SYSTEM_SCHEMA)]
[Meta<EnableStorage>(true)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.entity.appfield")]
internal class AppFieldEntity
{
    #region Base

    /// <summary>
    /// the application name
    /// </summary>
    [Meta<PrimaryIndex>(0)]
    [Meta<UniqueIndex>("seqno",0)]
    [Meta<SchemaType>(typeof(AppType))]
    public string App { get; set; } = string.Empty;

    /// <summary>
    /// The field name
    /// </summary>
    [Meta<PrimaryIndex>(1)]
    [Meta<SchemaType>(typeof(Identifier))]
    public string Name { get; set; } = default!;

    /// <summary>
    /// The seqno
    /// </summary>
    [Meta<UniqueIndex>("seqno", 1)]
    public int Seqno { get; set; }

    /// <summary>
    /// The field type
    /// </summary>
    [Meta<SchemaType>(typeof(Schema.ValueType))]
    public string Type { get; set; } = default!;

    #endregion

    #region Extension

    /// <summary>
    /// The extension properties of the node
    /// </summary>
    public JsonObject? Extensions { get; set; }
    
    #endregion

    #region Conversion

    public static implicit operator AppFieldEntity?(AppFieldSchema? appFieldSchema)
    {
        if  (appFieldSchema == null) return null;
        return new AppFieldEntity
        {
            App = appFieldSchema.App,
            Name = appFieldSchema.Name,
            Seqno = appFieldSchema.Seqno,
            Type = appFieldSchema.Type,
            Extensions = appFieldSchema.Extensions?.DeepClone() as JsonObject
        };
    }
    
    public static implicit operator AppFieldSchema?(AppFieldEntity? appFieldEntity)
    {
        if  (appFieldEntity == null) return null;
        return new AppFieldSchema
        {
            App = appFieldEntity.App,
            Name = appFieldEntity.Name,
            Seqno = appFieldEntity.Seqno,
            Type = appFieldEntity.Type,
            Extensions = appFieldEntity.Extensions?.DeepClone() as JsonObject
        };
    }

    #endregion    
}
