using FamilyExpenses.Api.Models;
using FamilyExpenses.Api.Repositories;
using FamilyExpenses.Api.Security;
using static FamilyExpenses.Api.Models.ResponseMapper;

namespace FamilyExpenses.Api.Services;

public interface IAuthService
{
    (AuthResponse? Response, ApiError? Error) Register(RegisterRequest request, HttpContext http);
    AuthResponse? Login(LoginRequest request, HttpContext http);
    TokenPairResponse? Refresh(RefreshRequest request, HttpContext http);
    bool Logout(RefreshRequest request);
}

public sealed class AuthService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    ICurrentUserService currentUser,
    ITokenService tokens) : IAuthService
{
    public (AuthResponse? Response, ApiError? Error) Register(RegisterRequest request, HttpContext http)
    {
        if (users.EmailExists(request.Email))
        {
            return (null, ApiError.Validation([new ErrorDetail("email", "Email already exists")]));
        }

        var user = users.Add(request.Email, PasswordHasher.Hash(request.Password), request.DisplayName);
        var pair = tokens.IssuePair(user.Id, http);

        return (new AuthResponse(ToUserShort(user), pair.AccessToken, pair.RefreshToken), null);
    }

    public AuthResponse? Login(LoginRequest request, HttpContext http)
    {
        var user = users.GetActiveByEmail(request.Email);
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return null;
        }

        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = user.LastLoginAt.Value;
        user = users.Update(user);
        var pair = tokens.IssuePair(user.Id, http);

        return new AuthResponse(ToUserShort(user), pair.AccessToken, pair.RefreshToken);
    }

    public TokenPairResponse? Refresh(RefreshRequest request, HttpContext http)
    {
        var refresh = refreshTokens.GetValidByHash(TokenService.HashRefreshToken(request.RefreshToken));
        if (refresh is null || users.GetById(refresh.UserId) is not { IsActive: true } user)
        {
            return null;
        }

        refreshTokens.Revoke(refresh.Id, DateTime.UtcNow);
        var pair = tokens.IssuePair(user.Id, http);

        return new TokenPairResponse(pair.AccessToken, pair.RefreshToken);
    }

    public bool Logout(RefreshRequest request)
    {
        if (!currentUser.TryGetUserId(out var userId))
        {
            return false;
        }

        var refresh = refreshTokens.GetByUserAndHash(userId, TokenService.HashRefreshToken(request.RefreshToken));
        if (refresh is not null)
        {
            refreshTokens.Revoke(refresh.Id, DateTime.UtcNow);
        }

        return true;
    }
}
