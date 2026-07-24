using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Scalar;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using SchemaKind = SchemaNode.Property.Record.SchemaKind;
using String = SchemaNode.Scalar.String;
using SchemaNode.Function;
using SchemaNode.Property;
using SchemaNode.Property.Constraint;
using SchemaNode.Runtime;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

/**
 * The application schema
 */
[Meta<SchemaKind>(SCHEMA_KIND_APP, SCHEMA_KIND_ORDER_APP)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.schema")]
[Meta<Append>(typeof(Display), typeof(Description), typeof(Relations))]
public sealed class AppSchema: PropertyOwner, IErrorProvider
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
    /// The full name of the app
    /// </summary>
    [SchemaIgnore]
    [JsonIgnore]
    public string FullName => $"{Container}.{Name}".Trim('.');

    #region Details

    /// <summary>
    /// Whether it has sub-applications
    /// </summary>
    [SchemaIgnore]
    public bool? HasApps { get; set; }
    
    /// <summary>
    /// Whether it has fields
    /// </summary>
    [SchemaIgnore]
    public bool? HasFields { get; set; }

    /// <summary>
    /// The sub applications
    /// </summary>
    [SchemaIgnore]
    public AppSchema[]? Apps { get; internal set; }
    
    /// <summary>
    /// The application fields
    /// </summary>
    [SchemaIgnore]
    public AppFieldSchema[]? Fields { get; set; }
    
    /// <summary>
    /// The application workflows
    /// </summary>
    [SchemaIgnore]
    public AppWorkflowSchema[]? Workflows { get; set; }

    /// <summary>
    /// The types related to the application
    /// </summary>
    [SchemaIgnore]
    public NodeSchema[]? NodeSchemas { get; set; }

    #endregion

    #region Status
    
    /// <summary>
    /// The app schema provider
    /// </summary>
    [SchemaIgnore]
    public Type? Provider { get; internal set; }

    /// <summary>
    /// The load state
    /// </summary>
    [SchemaIgnore]
    public SchemaLoadState? LoadState { get; set; }
    
    /// <summary>
    /// The error status
    /// </summary>
    [SchemaIgnore]
    public string? Error { get; set; }

    #endregion
}

/// <summary>
/// The application type, used for the parent app reference and app type definition, it's a string with format of {appnamespace}.{appname}
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.type")]
[Meta<UpLimitString>(PRIMARY_KEY_MAX_LEN)]
[Meta<EntrySource>($"{NS_SYSTEM_SCHEMA_REFLECT_APP}.{nameof(SystemAppReflect.getapps)}", NODE_SELF)]
public sealed class AppType : String;
