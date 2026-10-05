using System.ComponentModel.DataAnnotations;

namespace FamilyExpenses.Api.Models;

public record RegisterRequest([Required, EmailAddress] string Email, [Required] string Password, string? DisplayName);
public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);
public record RefreshRequest([Required] string RefreshToken);
public record UpdateUserRequest(string? DisplayName, [EmailAddress] string? Email, string? CurrentPassword, string? NewPassword);
public record CreateCategoryRequest([Required, StringLength(150, MinimumLength = 1)] string Name, bool? IsActive);
public record UpdateCategoryRequest([StringLength(150, MinimumLength = 1)] string? Name, bool? IsActive);
public record CreateSubcategoryRequest([Required] long CategoryId, [Required, StringLength(150, MinimumLength = 1)] string Name, bool? IsActive);
public record UpdateSubcategoryRequest(long? CategoryId, [StringLength(150, MinimumLength = 1)] string? Name, bool? IsActive);
public record CreateCurrencyRequest([Required, StringLength(3, MinimumLength = 3)] string Code, [Required, StringLength(150, MinimumLength = 1)] string Name, [Range(0, 8)] int? MinorUnit);
public record CreateOperationRequest([Required] long CategoryId, long? SubcategoryId, [Range(0, double.MaxValue)] decimal OriginalAmount, [Required, StringLength(3, MinimumLength = 3)] string CurrencyCode, string? Comment);
public record UpdateOperationRequest(long? CategoryId, long? SubcategoryId, [Range(0, double.MaxValue)] decimal? OriginalAmount, [StringLength(3, MinimumLength = 3)] string? CurrencyCode, string? Comment);
