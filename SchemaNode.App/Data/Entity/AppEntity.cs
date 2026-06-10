using SchemaNode.Attribute;
using SchemaNode.Property.App;
using SchemaNode.Property.Core;
using SchemaNode.Scalar;
using SchemaNode.Schema;
using System.Text.Json.Nodes;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;


namespace SchemaNode.Data.Entity;

[Meta<App>($"{NS_SYSTEM_SCHEMA}")]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.entity.app")]
internal class AppEntity
{
    /// <summary>
    /// The container app name
    /// </summary>
    [Meta<PrimaryIndex>(0)]
    [Meta<SchemaType>(typeof(AppType))]
    public string? Container { get; set; }

    /// <summary>
    /// The application name
    /// </summary>
    [Meta<PrimaryIndex>(1)]
    [Meta<SchemaType>(typeof(Identifier))]
    public string Name { get; set; } = default!;

    /// <summary>
    /// The extension properties of the node
    /// </summary>
    public JsonObject? Extensions { get; set; }

    #region Conversion

    #endregion
}
