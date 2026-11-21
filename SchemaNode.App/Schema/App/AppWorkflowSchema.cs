using SchemaNode.Attribute;
using SchemaNode.Property;
using SchemaNode.Property.App;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Property.String;
using SchemaNode.Property.Struct;
using SchemaNode.Runtime;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using SchemaKind = SchemaNode.Property.Record.SchemaKind;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The application workflow reference
/// </summary>
[Meta<SchemaKind>(SCHEMA_KIND_APP_WORKFLOW, SCHEMA_KIND_ORDER_APP_WORKFLOW)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_WORKFLOW}.schema")]
[Meta<Append>(typeof(Display), typeof(Description))]
[Meta<Attach>(SCHEMA_KIND_APP_WORKFLOW)]
[Meta<AppProvider>(nameof(App))]
public sealed class AppWorkflowSchema: PropertyOwner
{
    /// <summary>
    /// the application name
    /// </summary>
    [Meta<PrimaryIndex>(0)]
    [Meta<SchemaType>(typeof(AppType))]
    [Meta<ReadOnly>(true)]
    [Meta<InVisible>(true)]
    public string App { get; set; } = string.Empty;

    /// <summary>
    /// The work flow name
    /// </summary>
    [Meta<PrimaryIndex>(1)]
    [Meta<UpLimitString>(ENTITY_PRIMARY_KEY_MAX_LEN)]
    public string Name { get; set; } = default!;
    
    /// <summary>
    /// The seqno
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    public int Seqno { get; set; }
    
    /// <summary>
    /// Active the workflow
    /// </summary>
    public bool Active { get; set; }

    /// <summary>
    /// The referenced workflow type
    /// </summary>
    [Meta<SchemaType>(typeof(WorkflowType))]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Combine other workflow schema
    /// </summary>
    public bool Combine(AppWorkflowSchema? other, ISchemaRuntime? runtime = null)
    {
        return other is not null && other.Name.Equals(Name, StringComparison.OrdinalIgnoreCase);
    }
}
