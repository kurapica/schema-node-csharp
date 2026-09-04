using SchemaNode.Context;
using SchemaNode.Runtime;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Property.Object;

/// <summary>
/// The self access path, which indicates that the node can access itself
/// </summary>
public class SelfAccessPath: IValueAccessPathHandler
{
    /// <inheritdoc/>
    public Task LoadAsync(ISchemaContext context) => Task.CompletedTask;

    /// <inheritdoc/>
    public string Path => NODE_SELF;

    /// <inheritdoc/>
    public LocaleString Display => nameof(NODE_SELF);
    
    /// <inheritdoc/>
    public IValueTypeAccess? GetAccessValueType(IValueTypeAccess owner) => owner;

    /// <inheritdoc/>
    public IValueAccess? GetAccessValue(IValueAccess owner, IValueAccess? node = null) => node ?? owner;
}