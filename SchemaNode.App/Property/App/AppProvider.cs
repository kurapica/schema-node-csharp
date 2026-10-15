using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Core;
using SchemaNode.Property.Property;
using SchemaNode.Runtime;
using SchemaNode.Struct;
using static SchemaNode.Utility.AppConstant;
using static SchemaNode.Utility.Constant;
using SchemaType = SchemaNode.Property.Core.SchemaType;

namespace SchemaNode.Property.App;

/// <summary>
/// The app provider
/// </summary>
[Meta<ForSchema>(SCHEMA_KIND_NODE_STRUCT)]
[Meta<OfNodeKind>(NODE_KIND_PROPERTY)]
[Meta<SchemaType>($"{NS_SYSTEM_SCHEMA_PROPERTY_APP}.{nameof(AppProvider)}")]
public sealed class AppProvider : Property<string>;

/// <summary>
/// The app provider access path, which indicates the app declare node form parent nodes
/// </summary>
public class AppProviderAccessPath : IValueAccessPathHandler
{
    private IValueTypeAccess? _valueType;

    /// <inheritdoc/>
    public string Path => APP_PROVIDER;

    /// <inheritdoc/>
    public LocaleString Display => nameof(APP_PROVIDER);

    /// <inheritdoc/>
    public async Task LoadAsync(ISchemaContext context)
        => _valueType = context is SchemaContext ctx
                ? await ctx.GetNodeTypeAsync<StringType>($"{NS_SYSTEM_SCHEMA_APP}.type")
                : null;

    /// <inheritdoc/>
    public IValueTypeAccess? GetAccessValueType(IValueTypeAccess owner) => _valueType;

    /// <inheritdoc/>
    public IValueAccess? GetAccessValue(IValueAccess owner, IValueAccess? node = null)
    {
        var access = node ?? owner;
        AppProvider? appProvider = null;
        while (access != null)
        {
            appProvider = access.PropertyProvider?.GetProperty<AppProvider>();
            if (appProvider is { HasValue: true }) break;
            access = access.Parent;
        }

        return appProvider is { HasValue: true }
            ? access?.GetAccessValue(appProvider.GetValue<string>()!, node)
            : null;
    }
}
