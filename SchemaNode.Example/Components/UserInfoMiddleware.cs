namespace SchemaNode.Example.Components;

public class UserInfoMiddleware(RequestDelegate next)
{
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