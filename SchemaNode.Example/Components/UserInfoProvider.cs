using SchemaNode.Context;

namespace SchemaNode.Example.Components;

/// <summary>
/// The user info provider
/// </summary>
public class UserInfoProvider(UserInfo? userInfo): ISchemaContextItemProvider<UserInfo>
{
    /// <inheritdoc/>
    public bool HasItem => userInfo != null ;

    /// <inheritdoc/>
    public UserInfo GetItem()
    {
        return userInfo ?? throw new InvalidOperationException("UserInfo is not available");
    }
}