using SchemaNode.Components.Context;

namespace SchemaNode.Example.Components;

/// <summary>
/// The user info provider
/// </summary>
public class UserInfoProvider(UserInfo? userInfo): ISchemaContextItemProvider<UserInfo>
{
    public bool HasItem => userInfo != null ;
    public UserInfo GetItem()
    {
        return userInfo ?? throw new InvalidOperationException("UserInfo is not available");
    }
}