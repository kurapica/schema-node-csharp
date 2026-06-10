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

[Meta<App>($"{NS_SYSTEM_SCHEMA}")]
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

    #region Source Push

    /// <summary>
    /// The input source field
    /// </summary>
    [Meta<SchemaType>(typeof(Identifier))]
    public string? Source { get; set; }

    /// <summary>
    /// The push function, convert the input data to the type data
    /// </summary>
    [Meta<SchemaType>(typeof(FuncType))]
    public string? Push { get; set; }

    /// <summary>
    /// The combine rule for scalar/enum type
    /// </summary>
    public DataCombineType? Combine { get; set; }

    /// <summary>
    /// The combine rule for struct or struct-array type
    /// </summary>
    public DataCombine[]? Combines { get; set; }

    #endregion

    #region Foreign & View

    /// <summary>
    /// The foreign key settings
    /// </summary>
    public Foreign[]? Foreigns { get; set; }

    /// <summary>
    /// The field view settings
    /// </summary>
    public FieldView? View { get; set; }

    #endregion

    /// <summary>
    /// The extension properties of the node
    /// </summary>
    public JsonObject? Extensions { get; set; }
}
