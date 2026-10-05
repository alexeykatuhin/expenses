namespace FamilyExpenses.Api.Models;

public enum EntryKind
{
    Income,
    Expense
}

public sealed class User(long id, string email, string passwordHash, string? displayName, bool isActive, DateTime createdAt, DateTime updatedAt, DateTime? lastLoginAt)
{
    public long Id { get; } = id;
    public string Email { get; set; } = email;
    public string PasswordHash { get; set; } = passwordHash;
    public string? DisplayName { get; set; } = displayName;
    public bool IsActive { get; set; } = isActive;
    public DateTime CreatedAt { get; } = createdAt;
    public DateTime UpdatedAt { get; set; } = updatedAt;
    public DateTime? LastLoginAt { get; set; } = lastLoginAt;
}

public sealed class RefreshToken(long id, long userId, string tokenHash, string? userAgent, string? ipAddress, DateTime expiresAt, DateTime? revokedAt, DateTime createdAt)
{
    public long Id { get; } = id;
    public long UserId { get; } = userId;
    public string TokenHash { get; } = tokenHash;
    public string? UserAgent { get; } = userAgent;
    public string? IpAddress { get; } = ipAddress;
    public DateTime ExpiresAt { get; } = expiresAt;
    public DateTime? RevokedAt { get; set; } = revokedAt;
    public DateTime CreatedAt { get; } = createdAt;
}

public sealed class Category(long id, long userId, EntryKind kind, string name, bool isActive)
{
    public long Id { get; } = id;
    public long UserId { get; } = userId;
    public EntryKind Kind { get; } = kind;
    public string Name { get; set; } = name;
    public bool IsActive { get; set; } = isActive;
}

public sealed class Subcategory(long id, long userId, EntryKind kind, long categoryId, string name, bool isActive)
{
    public long Id { get; } = id;
    public long UserId { get; } = userId;
    public EntryKind Kind { get; } = kind;
    public long CategoryId { get; set; } = categoryId;
    public string Name { get; set; } = name;
    public bool IsActive { get; set; } = isActive;
}

public sealed class ReportingPeriod(long id, long userId, DateOnly startDate, DateOnly endDate, bool isClosed, DateTime? closedAt, DateTime createdAt)
{
    public long Id { get; } = id;
    public long UserId { get; } = userId;
    public DateOnly StartDate { get; } = startDate;
    public DateOnly EndDate { get; } = endDate;
    public bool IsClosed { get; set; } = isClosed;
    public DateTime? ClosedAt { get; set; } = closedAt;
    public DateTime CreatedAt { get; } = createdAt;
}

public sealed class Currency(string code, string name, int? minorUnit, bool isActive)
{
    public string Code { get; } = code;
    public string Name { get; } = name;
    public int? MinorUnit { get; } = minorUnit;
    public bool IsActive { get; } = isActive;
}

public sealed class CurrencyUsage(Currency currency, DateTime? lastUsedAt)
{
    public Currency Currency { get; } = currency;
    public DateTime? LastUsedAt { get; } = lastUsedAt;
}

public sealed class ConvertedMoney(decimal amountRub, decimal amountUsd, DateOnly? exchangeRateDate, decimal? rubRate, decimal? usdRate)
{
    public decimal AmountRub { get; } = amountRub;
    public decimal AmountUsd { get; } = amountUsd;
    public DateOnly? ExchangeRateDate { get; } = exchangeRateDate;
    public decimal? RubRate { get; } = rubRate;
    public decimal? UsdRate { get; } = usdRate;
}

public sealed class Operation(
    long id,
    long userId,
    EntryKind kind,
    long categoryId,
    long? subcategoryId,
    long? periodId,
    decimal originalAmount,
    string currencyCode,
    decimal amountRub,
    decimal amountUsd,
    DateOnly? exchangeRateDate,
    decimal? rubRate,
    decimal? usdRate,
    DateOnly operationDate,
    string? comment,
    DateTime createdAt,
    DateTime updatedAt)
{
    public long Id { get; } = id;
    public long UserId { get; } = userId;
    public EntryKind Kind { get; } = kind;
    public long CategoryId { get; set; } = categoryId;
    public long? SubcategoryId { get; set; } = subcategoryId;
    public long? PeriodId { get; set; } = periodId;
    public decimal OriginalAmount { get; set; } = originalAmount;
    public string CurrencyCode { get; set; } = currencyCode;
    public decimal AmountRub { get; set; } = amountRub;
    public decimal AmountUsd { get; set; } = amountUsd;
    public DateOnly? ExchangeRateDate { get; set; } = exchangeRateDate;
    public decimal? RubRate { get; set; } = rubRate;
    public decimal? UsdRate { get; set; } = usdRate;
    public DateOnly OperationDate { get; set; } = operationDate;
    public string? Comment { get; set; } = comment;
    public DateTime CreatedAt { get; } = createdAt;
    public DateTime UpdatedAt { get; set; } = updatedAt;
}
