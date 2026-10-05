using System.Security.Claims;
using FamilyExpenses.Api.Repositories;

namespace FamilyExpenses.Api.Security;

public sealed class AccessTokenMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITokenService tokens, IUserRepository users)
    {
        var auth = context.Request.Headers.Authorization.ToString();

        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) &&
            tokens.TryReadAccessToken(auth["Bearer ".Length..], out var userId) &&
            users.GetById(userId) is { IsActive: true })
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                "Bearer"));
        }

        await next(context);
    }
}
