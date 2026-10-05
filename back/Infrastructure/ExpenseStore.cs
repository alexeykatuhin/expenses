using System.Collections.Concurrent;
using FamilyExpenses.Api.Models;

namespace FamilyExpenses.Api.Infrastructure;

public sealed class ExpenseStore
{
    private long _userId;
    private long _refreshTokenId;
    private long _categoryId;
    private long _subcategoryId;
    private long _operationId;
    private long _reportingPeriodId;

    public ConcurrentDictionary<long, User> Users { get; } = new();
    public ConcurrentDictionary<long, RefreshToken> RefreshTokens { get; } = new();
    public ConcurrentDictionary<long, Category> Categories { get; } = new();
    public ConcurrentDictionary<long, Subcategory> Subcategories { get; } = new();
    public ConcurrentDictionary<long, Operation> Operations { get; } = new();
    public ConcurrentDictionary<long, ReportingPeriod> ReportingPeriods { get; } = new();
    public ConcurrentDictionary<string, Currency> Currencies { get; } = new(
        new[]
        {
            new KeyValuePair<string, Currency>("BYN", new Currency("BYN", "Belarusian Ruble", 2, true)),
            new KeyValuePair<string, Currency>("EUR", new Currency("EUR", "Euro", 2, true)),
            new KeyValuePair<string, Currency>("GBP", new Currency("GBP", "Pound Sterling", 2, true)),
            new KeyValuePair<string, Currency>("JPY", new Currency("JPY", "Japanese Yen", 0, true)),
            new KeyValuePair<string, Currency>("KHR", new Currency("KHR", "Riel", 2, true)),
            new KeyValuePair<string, Currency>("RUB", new Currency("RUB", "Russian Ruble", 2, true)),
            new KeyValuePair<string, Currency>("USD", new Currency("USD", "US Dollar", 2, true)),
            new KeyValuePair<string, Currency>("VND", new Currency("VND", "Vietnamese Dong", 0, true)),
        });

    public long NextUserId() => Interlocked.Increment(ref _userId);
    public long NextRefreshTokenId() => Interlocked.Increment(ref _refreshTokenId);
    public long NextCategoryId() => Interlocked.Increment(ref _categoryId);
    public long NextSubcategoryId() => Interlocked.Increment(ref _subcategoryId);
    public long NextOperationId() => Interlocked.Increment(ref _operationId);
    public long NextReportingPeriodId() => Interlocked.Increment(ref _reportingPeriodId);
}
