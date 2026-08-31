using SchemaNode.Attribute;
using static SchemaNode.Utility.AppConstant;
using SchemaKind = SchemaNode.Property.Record.SchemaKind;
using SchemaNode.Property.Core;
using SchemaNode.Scalar;
using SchemaNode.Property.Common;
using SchemaNode.Property;
using SchemaNode.Runtime;
using SchemaValueType = SchemaNode.Schema.ValueType;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/// <summary>
/// The application field schema
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP_FIELD}.schema")]
[Meta<SchemaKind>(SCHEMA_KIND_APP_FIELD, SCHEMA_KIND_ORDER_APP_FIELD)]
[Meta<Append>(typeof(Display), typeof(Description), typeof(Disable))]
[Meta<Attach>(SCHEMA_KIND_APP_FIELD)]
public sealed class AppFieldSchema: PropertyOwner, IErrorProvider
{
    #region Base
    
    /// <summary>
    /// the application name
    /// </summary>
    [Meta<PrimaryIndex>(0)]
    [Meta<SchemaType>(typeof(AppType))]
    [Meta<ReadOnly>(true)]
    [Meta<InVisible>(true)]
    public string App { get; set; } = string.Empty;

    /// <summary>
    /// The field name
    /// </summary>
    [Meta<PrimaryIndex>(1)]
    [Meta<SchemaType>(typeof(Identifier))]
    [Meta<Immutable>(true)]
    public string Name { get; set; } = default!;

    /// <summary>
    /// The seqno
    /// </summary>
    [SchemaIgnore]
    public int Seqno { get; set; }

    /// <summary>
    /// The field type
    /// </summary>
    [Meta<SchemaType>(typeof(SchemaValueType))]
    public string Type { get; set; } = default!;
    
    #endregion
    
    #region Status
    
    /// <summary>
    /// The error status
    /// </summary>
    [SchemaIgnore]
    public string? Error { get; set; }

    #endregion
}

#region Help Types


#endregion