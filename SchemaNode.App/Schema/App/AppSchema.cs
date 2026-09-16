using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Scalar;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;
using SchemaKind = SchemaNode.Property.Record.SchemaKind;
using String = SchemaNode.Scalar.String;
using SchemaNode.Function;
using SchemaNode.Property;
using SchemaNode.Property.String;
using SchemaNode.Relation;
using SchemaNode.Runtime;
using SchemaNode.Schema.Provider;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Schema;

[Meta<SchemaKind>(SCHEMA_KIND_APP, SCHEMA_KIND_ORDER_APP)]
[Meta<Append>(typeof(Display), typeof(Description), typeof(Relations), typeof(SystemDefined))]
public sealed class AppKind;

/// <summary>
/// The application schema
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.schema")]
[Meta<EntrySourceProvider>($"{NS_SYSTEM_SCHEMA_REFLECT_APP}.{nameof(SystemReflectApp.getaccessentries)}", $"@{nameof(Container)}", $"@{nameof(Name)}", NODE_SELF)]
[Meta<AccessValueTypeProvider>($"{NS_SYSTEM_SCHEMA_REFLECT_APP}.{nameof(SystemReflectApp.getaccessvaluetype)}",  $"@{nameof(Container)}", $"@{nameof(Name)}", NODE_SELF)]
public sealed class AppSchema: PropertyOwner, IErrorProvider
{
    /// <summary>
    /// The container app name
    /// </summary>
    [Meta<PrimaryIndex>(0)]
    [Meta<SchemaType>(typeof(AppType))]
    [Relation<ReadOnly, Call>(nameof(Container), $"{NS_SYSTEM_LOGIC}.{nameof(SystemLogic.notempty)}", $"@{nameof(Name)}")]
    public string? Container { get; set; }

    /// <summary>
    /// The application name
    /// </summary>
    [Meta<PrimaryIndex>(1)]
    [Meta<SchemaType>(typeof(Identifier))]
    public required string Name { get; set; }

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
    public IAppEntryProvider? Provider { get; internal set; }

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

    #region Method

    /// <summary>
    /// Combine the app schema
    /// </summary>
    public bool Combine(AppSchema? other, ISchemaRuntime? runtime = null)
    {
        if (other is null || !other.Name.Equals(Name, StringComparison.OrdinalIgnoreCase)) return false;
        CombineProperties(other, runtime, SCHEMA_KIND_APP);
        
        // combine
        if (other.Apps is { Length: > 0 })
        {
            if (Apps == null || Apps.Length == 0)
            {
                Apps = other.Apps.ToArray();
            }
            else
            {
                Apps = Apps.Concat(other.Apps.Where(a => !Apps.Any(e => e.Combine(a))).ToArray()).ToArray();
            }
        }

        // Combine fields
        if (other.Fields is { Length: > 0 })
        {
            Fields = Fields == null || Fields.Length == 0
                ? other.Fields 
                : Fields.Concat(other.Fields.Where(f => !Fields.Any((e => e.Combine(f))))
                    .ToArray()).ToArray();
        }

        // Combine workflow
        if (other.Workflows is { Length: > 0 })
        {
            Workflows = Workflows == null || Workflows.Length == 0
                ? other.Workflows
                : Workflows.Concat(other.Workflows.Where(w => !Workflows.Any(e => e.Combine(w)))
                    .ToArray()).ToArray();
        }
        return true;
    }

    #endregion
}

/// <summary>
/// The application type, used for the parent app reference and app type definition, it's a string with format of {appnamespace}.{appname}
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.type")]
[Meta<UpLimitString>(PRIMARY_KEY_MAX_LEN)]
[Meta<EntrySource>($"{NS_SYSTEM_SCHEMA_REFLECT_APP}.{nameof(SystemReflectApp.getappentries)}", NODE_SELF)]
public sealed class AppType : String;
