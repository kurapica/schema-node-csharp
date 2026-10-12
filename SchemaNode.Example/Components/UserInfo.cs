using SchemaNode.Attribute;
using SchemaNode.Property.Core;

namespace SchemaNode.Example.Components;

/// <summary>
/// User Info
/// </summary>
[Meta<SchemaType>("example.user")]
public class UserInfo
{
    /// <summary>
    /// User ID
    /// </summary>
    public string? Id { get; set; }
}