using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Common;
using SchemaNode.Property.Core;
using SchemaNode.Property.String;
using SchemaNode.Relation;
using SchemaNode.Runtime;
using SchemaType = SchemaNode.Property.Core.SchemaType;
using static SchemaNode.Utility.Constant;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace SchemaNode.Function;

/// <summary>
/// The system.data api
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_DATA)]
public static class SystemData
{
    /// <summary>
    /// Gets the context item
    /// </summary>
    [Relation<Valid, Assign>(nameof(access), $"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(Reflect.Type.isaccessassignableto)}", NS_SYSTEM_CONTEXT, NODE_SELF, false, $"@{FUNC_RETURN}")]
    public static T? getcontext<T>(
        SchemaContext context,
        
        [Meta<EntrySource>($"{NS_SYSTEM_SCHEMA_REFLECT_TYPE}.{nameof(Reflect.Type.getaccessentries)}", NS_SYSTEM_CONTEXT, NODE_SELF)] 
        string access)
    {
        IValueAccess? item = context.GetContextItem(access);
        return item != null ? item.GetValue<T>() : default(T?);
    }
}