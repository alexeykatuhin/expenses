using System.Globalization;

namespace FamilyExpenses.Api.Models;

public record AuthResponse(UserShortResponse User, string AccessToken, string RefreshToken);
public record TokenPairResponse(string AccessToken, string RefreshToken);
public record UserShortResponse(long Id, string Email, string? DisplayName, bool IsActive);
public record UserDetailsResponse(long Id, string Email, string? DisplayName, bool IsActive, DateTime? LastLoginAt, DateTime CreatedAt, DateTime UpdatedAt);
public record CategoryResponse(long Id, string Name, bool IsActive);
public record SubcategoryResponse(long Id, long CategoryId, string Name, bool IsActive);
public record ReportingPeriodResponse(long Id, DateOnly StartDate, DateOnly EndDate, bool IsClosed, DateTime? ClosedAt, DateTime CreatedAt);
public record CurrencyResponse(string Code, string Name, int? MinorUnit, DateTime? LastUsedAt);
public record OperationResponse(long Id, long CategoryId, long? SubcategoryId, long? PeriodId, string OriginalAmount, string CurrencyCode, string AmountRub, string AmountUsd, DateOnly? ExchangeRateDate, string? RubRate, string? UsdRate, DateOnly OperationDate, string? Comment, DateTime CreatedAt, DateTime UpdatedAt);
public record OperationFeedResponse(string Type, long Id, long CategoryId, long? SubcategoryId, long? PeriodId, string OriginalAmount, string CurrencyCode, string AmountRub, string AmountUsd, DateOnly? ExchangeRateDate, string? RubRate, string? UsdRate, DateOnly OperationDate, string? Comment);
public record PageResponse<T>(IReadOnlyCollection<T> Items, int Limit, int Offset, int Total);
public record SummaryResponse(MoneyResponse Income, MoneyResponse Expense, MoneyResponse Balance);
public record GroupReportResponse(long? Id, string AmountRub, string AmountUsd);
public record PeriodCategoryReportResponse(DateOnly PeriodStart, DateOnly PeriodEnd, long? Id, string AmountRub, string AmountUsd);
public record IssuedTokenPair(string AccessToken, string RefreshToken);
public record AccessTokenPayload(long UserId, long ExpiresAtUnix);
public record ErrorDetail(string Field, string Message);
public record ErrorBody(string Code, string Message, IReadOnlyCollection<ErrorDetail> Details);

public sealed class MoneyResponse(decimal rub, decimal usd)
{
    public string AmountRub { get; } = ApiFormat.Money(rub);
    public string AmountUsd { get; } = ApiFormat.Money(usd);
}

public record ApiError(ErrorBody Error)
{
    public static ApiError Validation(IReadOnlyCollection<ErrorDetail> details) =>
        new(new ErrorBody("VALIDATION_ERROR", "Request data is invalid", details));
}

public static class ApiFormat
{
    public static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    public static string Rate(decimal value) => value.ToString("0.########", CultureInfo.InvariantCulture);
}

public static class ResponseMapper
{
    public static UserShortResponse ToUserShort(User user) => new(user.Id, user.Email, user.DisplayName, user.IsActive);

    public static UserDetailsResponse ToUserDetails(User user) =>
        new(user.Id, user.Email, user.DisplayName, user.IsActive, user.LastLoginAt, user.CreatedAt, user.UpdatedAt);

    public static CategoryResponse ToCategoryResponse(Category category) => new(category.Id, category.Name, category.IsActive);

    public static SubcategoryResponse ToSubcategoryResponse(Subcategory subcategory) =>
        new(subcategory.Id, subcategory.CategoryId, subcategory.Name, subcategory.IsActive);

    public static ReportingPeriodResponse ToReportingPeriodResponse(ReportingPeriod period) =>
        new(period.Id, period.StartDate, period.EndDate, period.IsClosed, period.ClosedAt, period.CreatedAt);

    public static CurrencyResponse ToCurrencyResponse(CurrencyUsage usage) =>
        new(usage.Currency.Code, usage.Currency.Name, usage.Currency.MinorUnit, usage.LastUsedAt);

    public static OperationResponse ToOperationResponse(Operation operation) => new(
        operation.Id,
        operation.CategoryId,
        operation.SubcategoryId,
        operation.PeriodId,
        ApiFormat.Money(operation.OriginalAmount),
        operation.CurrencyCode,
        ApiFormat.Money(operation.AmountRub),
        ApiFormat.Money(operation.AmountUsd),
        operation.ExchangeRateDate,
        operation.RubRate is null ? null : ApiFormat.Rate(operation.RubRate.Value),
        operation.UsdRate is null ? null : ApiFormat.Rate(operation.UsdRate.Value),
        operation.OperationDate,
        operation.Comment,
        operation.CreatedAt,
        operation.UpdatedAt);

    public static OperationFeedResponse ToOperationFeed(Operation operation) => new(
        KindName(operation.Kind),
        operation.Id,
        operation.CategoryId,
        operation.SubcategoryId,
        operation.PeriodId,
        ApiFormat.Money(operation.OriginalAmount),
        operation.CurrencyCode,
        ApiFormat.Money(operation.AmountRub),
        ApiFormat.Money(operation.AmountUsd),
        operation.ExchangeRateDate,
        operation.RubRate is null ? null : ApiFormat.Rate(operation.RubRate.Value),
        operation.UsdRate is null ? null : ApiFormat.Rate(operation.UsdRate.Value),
        operation.OperationDate,
        operation.Comment);

    public static GroupReportResponse ToGroupReport(long? id, decimal rub, decimal usd) =>
        new(id, ApiFormat.Money(rub), ApiFormat.Money(usd));

    public static PeriodCategoryReportResponse ToPeriodCategoryReport(DateOnly periodStart, DateOnly periodEnd, long? id, decimal rub, decimal usd) =>
        new(periodStart, periodEnd, id, ApiFormat.Money(rub), ApiFormat.Money(usd));

    public static string KindName(EntryKind kind) => kind == EntryKind.Income ? "income" : "expense";
}
