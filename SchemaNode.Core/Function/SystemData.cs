using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Schema;
using static SchemaNode.Utility.Constant;
using SchemaType = SchemaNode.Property.Core.SchemaType;
using EnumType = SchemaNode.Runtime.EnumType;
using SchemaNode.Struct;

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
    public static DataNode? getcontext(SchemaContext context, string access) => context.GetContextItem(access);
}