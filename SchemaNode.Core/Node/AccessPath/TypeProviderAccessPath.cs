using SchemaNode.Context;
using SchemaNode.Property.Core;
using SchemaNode.Runtime;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;
using ValueType = SchemaNode.Runtime.ValueType;

namespace SchemaNode.Property.Object;

/// <summary>
/// The type provider access path, which indicates the type declare node form parent nodes
/// </summary>
public class TypeProviderAccessPath: IValueAccessPathHandler
{
    private IValueTypeAccess? _valueType;

    /// <inheritdoc/>
    public string Path => TYPE_PROVIDER;
    
    /// <inheritdoc/>
    public LocaleString Display => nameof(TYPE_PROVIDER);

    /// <inheritdoc/>
    public async Task LoadAsync(ISchemaContext context)
        => _valueType = context is SchemaContext ctx 
                ? await ctx.GetNodeTypeAsync<ValueType>($"{NS_SYSTEM_SCHEMA_NODE}.valuetype")
                : null;

    /// <inheritdoc/>
    public IValueTypeAccess? GetAccessValueType(IValueTypeAccess owner) => _valueType;

    /// <inheritdoc/>
    public IValueAccess? GetAccessValue(IValueAccess owner, IValueAccess? node = null)
    {
        var access = node ?? owner;
        TypeProvider? typeProvider = null;
        while (access != null)
        {
            typeProvider = access.PropertyProvider?.GetProperty<TypeProvider>();
            if (typeProvider is { HasValue: true }) break;
            access = access.Parent;
        }
        
        return typeProvider is { HasValue: true } 
            ? access?.GetAccessValue(typeProvider.GetValue<string>()!, node) 
            : null;
    }
}