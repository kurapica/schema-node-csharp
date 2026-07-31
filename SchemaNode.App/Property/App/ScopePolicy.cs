using SchemaNode.Attribute;
using SchemaNode.Enum;
using SchemaNode.Property.Core;
using SchemaNode.Scalar;
using static SchemaNode.Utility.Constant;
using static SchemaNode.Utility.AppConstant;

namespace SchemaNode.Property.App;

/// <summary>
/// The application target policy
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_APP)]
[Meta<OfSchema>(SCHEMA_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_APP}.{nameof(ScopePolicy)}")]
public class ScopePolicy : Property<AppScopePolicy>
{
    public override void SetValue<TValue>(TValue value)
    {
        if (value is AppScopeType type)
            base.SetValue(new AppScopePolicy{ Type = type });
        else
            base.SetValue(value);
    }
}

/// <summary>
/// The app target policy
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.ScopePolicy")]
public sealed class AppScopePolicy: IEquatable<AppScopePolicy>
{
    /// <summary>
    /// The app target policy type
    /// </summary>
    public AppScopeType Type { get; set; }
    
    /// <summary>
    /// The context maps for the context item mapping when the target policy is IsolationContext, can be used for multiple context items mapping
    /// </summary>
    public AppScopeContextMap[]? ContextMaps { get; set; }
    
    public bool Equals(AppScopePolicy? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Type == other.Type && 
               ((ContextMaps == null && other.ContextMaps == null) || 
                (ContextMaps != null && other.ContextMaps != null && 
                 ContextMaps.SequenceEqual(other.ContextMaps)));
    }
}

/// <summary>
/// The application scope context map, used for the context item mapping when the target policy is IsolationContext
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_APP}.ScopeContextMap")]
public sealed class AppScopeContextMap: IEquatable<AppScopeContextMap>
{
    /// <summary>
    /// The context item
    /// </summary>
    [Meta<PrimaryIndex>]
    public required string ContextItem { get; set; }

    /// <summary>
    /// The map key
    /// </summary>
    [Meta<SchemaType>(typeof(Identifier))]
    public string? MapKey { get; set; }

    public bool Equals(AppScopeContextMap? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return ContextItem.Equals(other.ContextItem) &&
               (string.IsNullOrWhiteSpace(MapKey) 
                   ? string.IsNullOrWhiteSpace(other.MapKey)
                   : MapKey.Equals(other.MapKey));
    }
}

