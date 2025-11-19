using SchemaNode.Components;
using SchemaNode.Components.Context;

namespace SchemaNode.Example.Components;

/// <summary>
/// The user info provider
/// </summary>
public class UserInfoProvider: ISchemaContextItemProvider<UserInfo>
{
    private readonly UserInfo _user = new();
    
    public bool HasItem => true;
    public UserInfo GetItem()
    {
        return _user;
    }
}