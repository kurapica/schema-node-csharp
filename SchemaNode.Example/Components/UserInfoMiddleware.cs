namespace SchemaNode.Example.Components;

/// <summary>
/// User Info middleware
/// </summary>
/// <param name="next"></param>
public class UserInfoMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Invoke the middleware to handle the user info
    /// </summary>
    /// <param name="context"></param>
    /// <param name="user"></param>
    /// <returns></returns>
    public async Task Invoke(HttpContext context, UserInfo user)
    {
        if (context.Request.Headers.TryGetValue("accountId", out var accountId))
        {
            user.UserId = accountId;
            user.IsAdmin = accountId == "admin";
        }
        await next(context);
    }
}