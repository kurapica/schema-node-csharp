using SchemaNode.Attribute;

namespace SchemaNode.Example.Components;

[Schema("example.user", "user", Locale.ZH_CN, "用户信息")]
public class UserInfo
{
    [Schema(null, "id", Locale.ZH_CN, "用户ID")]
    public string? UserId { get; set; }

    [Schema(null, "name", Locale.ZH_CN, "用户名")]
    public string? UserName { get; set; } = "Test";
    
    public bool IsAdmin { get; set; }
}