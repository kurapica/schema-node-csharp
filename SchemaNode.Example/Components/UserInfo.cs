using SchemaNode.Attribute;
using SchemaNode.Property.Core;

namespace SchemaNode.Example.Components;

/// <summary>
/// 访问用户信息
/// </summary>
[Meta<SchemaType>("example.user")]
public class UserInfo
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public string? Id { get; set; }
}