using SchemaNode.Attribute;

namespace SchemaNode.Example.Components;

/// <summary>
/// The user info
/// </summary>
[Schema("example.user")]
public class UserInfo
{
    /// <summary>
    /// User ID
    /// </summary>
    [Schema]
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