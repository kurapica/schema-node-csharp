using SchemaNode.Attribute;

namespace SchemaNode.Example.Components;

[SchemaType("example.user", "user", Locale.ZH_CN, "用户信息")]
public class UserInfo
{
    [SchemaType(null, "id", Locale.ZH_CN, "用户ID")]
    public Guid UserId { get; set; } = Guid.NewGuid();

    [SchemaType(null, "name", Locale.ZH_CN, "用户名")]
    public string UserName { get; set; } = "Test";
}