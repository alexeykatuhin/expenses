using FamilyExpenses.Api.Models;
using FamilyExpenses.Api.Repositories;
using FamilyExpenses.Api.Security;
using static FamilyExpenses.Api.Models.ResponseMapper;

namespace FamilyExpenses.Api.Services;

public interface IUserService
{
    UserDetailsResponse? GetCurrent();
    (UserDetailsResponse? Response, ApiError? Error, bool Unauthorized) UpdateCurrent(UpdateUserRequest request);
}

public sealed class UserService(ICurrentUserService currentUser, IUserRepository users) : IUserService
{
    public UserDetailsResponse? GetCurrent() =>
        currentUser.TryGetCurrentUser(out var user) ? ToUserDetails(user) : null;

    public (UserDetailsResponse? Response, ApiError? Error, bool Unauthorized) UpdateCurrent(UpdateUserRequest request)
    {
        if (!currentUser.TryGetCurrentUser(out var user))
        {
            return (null, null, true);
        }

        var errors = new List<ErrorDetail>();
        if (!string.IsNullOrWhiteSpace(request.Email) &&
            !string.Equals(request.Email, user.Email, StringComparison.OrdinalIgnoreCase) &&
            users.EmailExists(request.Email, user.Id))
        {
            errors.Add(new ErrorDetail("email", "Email already exists"));
        }

        if (!string.IsNullOrEmpty(request.NewPassword) &&
            (string.IsNullOrEmpty(request.CurrentPassword) || !PasswordHasher.Verify(request.CurrentPassword, user.PasswordHash)))
        {
            errors.Add(new ErrorDetail("current_password", "Current password is invalid"));
        }

        if (errors.Count > 0)
        {
            return (null, ApiError.Validation(errors), false);
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            user.Email = request.Email.Trim().ToLowerInvariant();
        }

        if (request.DisplayName is not null)
        {
            user.DisplayName = request.DisplayName;
        }

        if (!string.IsNullOrEmpty(request.NewPassword))
        {
            user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        }

        user.UpdatedAt = DateTime.UtcNow;
        user = users.Update(user);
        return (ToUserDetails(user), null, false);
    }
}
