using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FamilyExpenses.Api.Models;
using FamilyExpenses.Api.Repositories;

namespace FamilyExpenses.Api.Security;

public interface ITokenService
{
    IssuedTokenPair IssuePair(long userId, HttpContext http);
    bool TryReadAccessToken(string token, out long userId);
}

public sealed class TokenService(IConfiguration configuration, IRefreshTokenRepository refreshTokens) : ITokenService
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(configuration["Auth:SigningKey"] ?? "local-development-signing-key-change-me");
    private readonly TimeSpan _accessLifetime = TimeSpan.FromMinutes(30);
    private readonly TimeSpan _refreshLifetime = TimeSpan.FromDays(30);

    public IssuedTokenPair IssuePair(long userId, HttpContext http)
    {
        var accessToken = CreateAccessToken(userId, DateTime.UtcNow.Add(_accessLifetime));
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        refreshTokens.Add(
            userId,
            HashRefreshToken(refreshToken),
            http.Request.Headers.UserAgent.ToString(),
            http.Connection.RemoteIpAddress?.ToString(),
            DateTime.UtcNow.Add(_refreshLifetime));

        return new IssuedTokenPair(accessToken, refreshToken);
    }

    public bool TryReadAccessToken(string token, out long userId)
    {
        userId = 0;
        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            return false;
        }

        var payloadBytes = Base64UrlDecode(parts[0]);
        var expectedSignature = Sign(payloadBytes);
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(parts[1]), expectedSignature))
        {
            return false;
        }

        var payload = JsonSerializer.Deserialize<AccessTokenPayload>(payloadBytes);
        if (payload is null || payload.ExpiresAtUnix < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            return false;
        }

        userId = payload.UserId;
        return true;
    }

    public static string HashRefreshToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }

    private string CreateAccessToken(long userId, DateTime expiresAt)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new AccessTokenPayload(userId, new DateTimeOffset(expiresAt).ToUnixTimeSeconds()));
        return $"{Base64UrlEncode(payload)}.{Convert.ToBase64String(Sign(payload))}";
    }

    private byte[] Sign(byte[] payload)
    {
        using var hmac = new HMACSHA256(_key);
        return hmac.ComputeHash(payload);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string text)
    {
        var base64 = text.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Convert.FromBase64String(base64);
    }
}
