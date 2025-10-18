using SchemaNode.Attribute;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using static SchemaNode.Utility.Extension;

namespace SchemaNode.Enum;

/// <summary>
/// Schema types.
/// </summary>
public enum SchemaType
{
    /// <summary>
    /// The namespace node
    /// </summary>
    Namespace,

    /// <summary>
    /// The scalar node
    /// </summary>
    Scalar,

    /// <summary>
    /// The num node
    /// </summary>
    Enum,

    /// <summary>
    /// The struct node
    /// </summary>
    Struct,

    /// <summary>
    /// The array node
    /// </summary>
    Array,
    
    /// <summary>
    /// The complex json node, we don't care how it organized
    /// Useful for entities defined in C#
    /// </summary>
    Json,

    /// <summary>
    /// The function node
    /// </summary>
    Func,
}