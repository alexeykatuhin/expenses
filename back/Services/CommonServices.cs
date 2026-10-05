using System.Security.Claims;
using System.Text.RegularExpressions;
using FamilyExpenses.Api.Models;
using FamilyExpenses.Api.Repositories;

namespace FamilyExpenses.Api.Services;

public interface ICurrentUserService
{
    bool TryGetUserId(out long userId);
    bool TryGetCurrentUser(out User user);
}

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor, IUserRepository users) : ICurrentUserService
{
    public bool TryGetUserId(out long userId)
    {
        var claim = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(claim, out userId);
    }

    public bool TryGetCurrentUser(out User user)
    {
        user = null!;
        return TryGetUserId(out var userId) && users.GetById(userId) is { } currentUser && Assign(currentUser, out user);
    }

    private static bool Assign(User source, out User target)
    {
        target = source;
        return true;
    }
}

public static class ServiceHelpers
{
    private static readonly Regex NumericNamePattern = new(@"^\d+(?:[.,]\d+)?$", RegexOptions.Compiled);

    public static EntryKind? ParseKind(string? value) => value?.ToLowerInvariant() switch
    {
        "income" => EntryKind.Income,
        "expense" => EntryKind.Expense,
        null or "" => null,
        _ => null
    };

    public static (List<T> Items, int Limit, int Offset) Page<T>(IReadOnlyCollection<T> items, int? limit, int? offset)
    {
        var safeLimit = Math.Clamp(limit ?? 50, 1, 200);
        var safeOffset = Math.Max(offset ?? 0, 0);
        return (items.Skip(safeOffset).Take(safeLimit).ToList(), safeLimit, safeOffset);
    }

    public static bool IsNumericName(string name) => NumericNamePattern.IsMatch(name.Trim());
}
