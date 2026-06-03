using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Core;
using SchemaNode.Scalar;
using SchemaNode.Schema;
using SchemaNode.Struct;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Function;

/// <summary>
/// The system reflect
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_SCHEMA_REFLECT)]
public static class SystemAppReflect
{
    public static async Task<Entry<string>[]> getapps(SchemaContext context,
        [Meta<SchemaType>(typeof(AppType))] string? root = null)
    {
        await Task.Yield();
        return [];
    }

    public static async Task<Entry<string>[]> getappfields(SchemaContext context,
        [Meta<SchemaType>(typeof(AppType))] string app)
    {
        await Task.Yield();
        return [];
    }
    
    public static async Task<string?> getappfieldtype(SchemaContext context,
        [Meta<SchemaType>(typeof(AppType))] string app,
        [Meta<SchemaType>(typeof(Identifier))] string field,
        bool elementType = false)
    {
        await Task.Yield();
        return null;
    }
}
