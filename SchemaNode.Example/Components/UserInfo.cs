using SchemaNode.Attribute;
using SchemaNode.Property.Core;

namespace SchemaNode.Example.Components;

/// <summary>
/// The user info
/// </summary>
[Meta<SchemaType>("example.user")]
public class UserInfo
{
    /// <summary>
    /// User ID
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// User Name
    /// </summary>
    public string? UserName { get; set; } = "Test";
    
    /// <summary>
    /// As Admin
    /// </summary>
    public bool IsAdmin { get; set; }
}