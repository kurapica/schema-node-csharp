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
}