using System.Text.Json.Serialization;
using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using SchemaKind = SchemaNode.Property.Record.SchemaKind;
using SchemaNode.Property.Core;
using SchemaNode.Scalar;
using SchemaNode.Property.Common;
using SchemaNode.Function;
using SchemaNode.Property.Constraint;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The application field schema
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.schema")]
[Meta<SchemaKind>(SCHEMA_KIND_APP_FIELD, SCHEMA_KIND_ORDER_APP_FIELD)]
[Meta<Append>(typeof(Display), typeof(ReadOnly), typeof(Disable))]
public sealed class AppFieldSchema: ExtensibleSchema
{
    #region Base
    
    /// <summary>
    /// the application name
    /// </summary>
    [Meta<PrimaryIndex>(0)]
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
    public int Seqno { get; set; }

    /// <summary>
    /// The field type
    /// </summary>
    [Meta<SchemaType>(typeof(ValueType))]
    public string Type { get; set; } = default!;
    
    #endregion
    
    #region Source Push
    
    /// <summary>
    /// The input source field
    /// </summary>
    [Meta<SchemaType>(typeof(Identifier))]
    [Meta<EntrySource>($"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemAppReflect.getappfields)}", $"${nameof(App)}")]
    public string? Source { get; set; }
    
    [Meta<SchemaType>(typeof(ValueType))]
    [Meta<DisplayOnly>(true)]
    [Meta<InVisible>(true)]
    [Relation<Default>($"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemAppReflect.getappfieldtype)}",  $"${nameof(App)}", $"${nameof(Source)}", true)]
    public string? SourceType { get; set; }

    /// <summary>
    /// The push function, convert the input data to the type data
    /// </summary>
    [Meta<SchemaType>(typeof(FuncType))]
    [Relation<Visible>($"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notempty)}", $"${nameof(Source)}")]
    [Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_ARGS, NODE_SELF, $"${nameof(SourceType)}")]
    [Meta<Valid>(NS_SYSTEM_SCHEMA_REFLECT_FUNC_WITH_RETURN, NODE_SELF, $"${nameof(Type)}", true)]
    public string? Push { get; set; }
    
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
}

#region Help Types

/// <summary>
/// The foreign settings
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.foreign")]
public sealed class Foreign
{
    /// <summary>
    /// The foreign app name
    /// </summary>
    [Meta<SchemaType>(typeof(AppType))]
    public string App { get; set; } = string.Empty;
    
    /// <summary>
    /// The field refer to the other app target
    /// </summary>
    [Meta<EntrySource>($"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemAppReflect.getappfields)}", $"${nameof(App)}")]
    public string Field { get; set; } = string.Empty;
}

[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.view")]
public sealed class FieldView
{
    /// <summary>
    /// The source application
    /// </summary>
    [Meta<SchemaType>(typeof(AppType))]
    public string App { get; set; } = string.Empty;

    /// <summary>
    /// The source field
    /// </summary>
    [Meta<EntrySource>($"{NS_SYSTEM_SCHEMA_REFLECT}.{nameof(SystemAppReflect.getappfields)}", $"${nameof(App)}")]
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// The target map field
    /// </summary>
    public string Map { get; set; } = string.Empty;

    [SchemaIgnore]
    [JsonIgnore]
    public AppType? AppType { get; set; }
}

#endregion
