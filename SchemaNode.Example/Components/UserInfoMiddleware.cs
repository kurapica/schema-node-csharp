namespace SchemaNode.Example.Components;

public class UserInfoMiddleware
{
    private readonly RequestDelegate _next;

    public UserInfoMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, UserInfo user)
    {
        if (context.Request.Headers.TryGetValue("accountId", out var accountId))
        {
            user.UserId = accountId;
            user.IsAdmin = accountId == "admin";
        }
        await _next(context);
    }
}