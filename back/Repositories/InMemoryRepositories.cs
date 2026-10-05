using FamilyExpenses.Api.Infrastructure;
using FamilyExpenses.Api.Models;

namespace FamilyExpenses.Api.Repositories;

public sealed class InMemoryUserRepository(ExpenseStore store) : IUserRepository
{
    public IReadOnlyCollection<User> GetAll() => store.Users.Values.ToList();

    public User? GetById(long id) => store.Users.TryGetValue(id, out var user) ? user : null;

    public User? GetActiveByEmail(string email) =>
        store.Users.Values.FirstOrDefault(x => string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase) && x.IsActive);

    public bool EmailExists(string email, long? exceptId = null) =>
        store.Users.Values.Any(x => x.Id != exceptId && string.Equals(x.Email, email.Trim(), StringComparison.OrdinalIgnoreCase));

    public User Add(string email, string passwordHash, string? displayName)
    {
        var now = DateTime.UtcNow;
        var user = new User(store.NextUserId(), email.Trim().ToLowerInvariant(), passwordHash, displayName, true, now, now, null);
        store.Users[user.Id] = user;
        return user;
    }

    public User Update(User user)
    {
        store.Users[user.Id] = user;
        return user;
    }
}

public sealed class InMemoryRefreshTokenRepository(ExpenseStore store) : IRefreshTokenRepository
{
    public RefreshToken? GetValidByHash(string tokenHash) =>
        store.RefreshTokens.Values.FirstOrDefault(x =>
            x.TokenHash == tokenHash &&
            x.RevokedAt is null &&
            x.ExpiresAt > DateTime.UtcNow);

    public RefreshToken? GetByUserAndHash(long userId, string tokenHash) =>
        store.RefreshTokens.Values.FirstOrDefault(x => x.UserId == userId && x.TokenHash == tokenHash);

    public RefreshToken Add(long userId, string tokenHash, string? userAgent, string? ipAddress, DateTime expiresAt)
    {
        var token = new RefreshToken(store.NextRefreshTokenId(), userId, tokenHash, userAgent, ipAddress, expiresAt, null, DateTime.UtcNow);
        store.RefreshTokens[token.Id] = token;
        return token;
    }

    public void Revoke(long id, DateTime revokedAt)
    {
        if (store.RefreshTokens.TryGetValue(id, out var token))
        {
            token.RevokedAt = revokedAt;
        }
    }
}

public sealed class InMemoryCategoryRepository(ExpenseStore store) : ICategoryRepository
{
    public IReadOnlyCollection<Category> GetByUser(long userId, EntryKind kind, bool? isActive = null) =>
        store.Categories.Values
            .Where(x => x.UserId == userId && x.Kind == kind)
            .Where(x => isActive is null || x.IsActive == isActive)
            .OrderBy(x => x.Name)
            .ToList();

    public Category? GetById(long userId, EntryKind kind, long id) =>
        store.Categories.Values.FirstOrDefault(x => x.Id == id && x.UserId == userId && x.Kind == kind);

    public bool NameExists(long userId, EntryKind kind, string name, long? exceptId = null) =>
        store.Categories.Values.Any(x =>
            x.UserId == userId &&
            x.Kind == kind &&
            x.Id != exceptId &&
            string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    public Category Add(long userId, EntryKind kind, string name, bool isActive)
    {
        var category = new Category(store.NextCategoryId(), userId, kind, name.Trim(), isActive);
        store.Categories[category.Id] = category;
        return category;
    }

    public Category Update(Category category)
    {
        store.Categories[category.Id] = category;
        return category;
    }
}

public sealed class InMemorySubcategoryRepository(ExpenseStore store) : ISubcategoryRepository
{
    public IReadOnlyCollection<Subcategory> GetByUser(long userId, EntryKind kind, long? categoryId = null, bool? isActive = null) =>
        store.Subcategories.Values
            .Where(x => x.UserId == userId && x.Kind == kind)
            .Where(x => categoryId is null || x.CategoryId == categoryId)
            .Where(x => isActive is null || x.IsActive == isActive)
            .OrderBy(x => x.Name)
            .ToList();

    public Subcategory? GetById(long userId, EntryKind kind, long id) =>
        store.Subcategories.Values.FirstOrDefault(x => x.Id == id && x.UserId == userId && x.Kind == kind);

    public bool NameExists(long userId, EntryKind kind, long categoryId, string name, long? exceptId = null) =>
        store.Subcategories.Values.Any(x =>
            x.UserId == userId &&
            x.Kind == kind &&
            x.CategoryId == categoryId &&
            x.Id != exceptId &&
            string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    public Subcategory Add(long userId, EntryKind kind, long categoryId, string name, bool isActive)
    {
        var subcategory = new Subcategory(store.NextSubcategoryId(), userId, kind, categoryId, name.Trim(), isActive);
        store.Subcategories[subcategory.Id] = subcategory;
        return subcategory;
    }

    public Subcategory Update(Subcategory subcategory)
    {
        store.Subcategories[subcategory.Id] = subcategory;
        return subcategory;
    }
}

public sealed class InMemoryOperationRepository(ExpenseStore store) : IOperationRepository
{
    public IReadOnlyCollection<Operation> GetByUser(long userId, EntryKind? kind = null, DateOnly? dateFrom = null, DateOnly? dateTo = null, long? categoryId = null, long? subcategoryId = null, long? periodId = null) =>
        store.Operations.Values
            .Where(x => x.UserId == userId)
            .Where(x => kind is null || x.Kind == kind)
            .Where(x => dateFrom is null || x.OperationDate >= dateFrom)
            .Where(x => dateTo is null || x.OperationDate <= dateTo)
            .Where(x => categoryId is null || x.CategoryId == categoryId)
            .Where(x => subcategoryId is null || x.SubcategoryId == subcategoryId)
            .Where(x => periodId is null || x.PeriodId == periodId)
            .OrderByDescending(x => x.OperationDate)
            .ThenByDescending(x => x.Id)
            .ToList();

    public Operation? GetById(long userId, EntryKind kind, long id) =>
        store.Operations.Values.FirstOrDefault(x => x.Id == id && x.UserId == userId && x.Kind == kind);

    public Operation Add(long userId, EntryKind kind, long categoryId, long? subcategoryId, long periodId, decimal originalAmount, string currencyCode, ConvertedMoney converted, DateOnly operationDate, string? comment)
    {
        var now = DateTime.UtcNow;
        var operation = new Operation(
            store.NextOperationId(),
            userId,
            kind,
            categoryId,
            subcategoryId,
            periodId,
            originalAmount,
            currencyCode.Trim().ToUpperInvariant(),
            converted.AmountRub,
            converted.AmountUsd,
            converted.ExchangeRateDate,
            converted.RubRate,
            converted.UsdRate,
            operationDate,
            comment,
            now,
            now);
        store.Operations[operation.Id] = operation;
        return operation;
    }

    public Operation Update(Operation operation)
    {
        store.Operations[operation.Id] = operation;
        return operation;
    }

    public bool Remove(long id) => store.Operations.TryRemove(id, out _);
}

public sealed class InMemoryCurrencyRepository(ExpenseStore store) : ICurrencyRepository
{
    public IReadOnlyCollection<CurrencyUsage> GetActiveWithLastUsage(long userId)
    {
        var usage = store.Operations.Values
            .Where(x => x.UserId == userId)
            .GroupBy(x => x.CurrencyCode)
            .ToDictionary(x => x.Key, x => (DateTime?)x.Max(y => y.CreatedAt));

        return store.Currencies.Values
            .Where(x => x.IsActive)
            .Select(x => new CurrencyUsage(x, usage.GetValueOrDefault(x.Code)))
            .OrderBy(x => x.LastUsedAt is null)
            .ThenByDescending(x => x.LastUsedAt)
            .ThenBy(x => x.Currency.Code)
            .ToList();
    }

    public Currency? GetActiveByCode(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return store.Currencies.TryGetValue(normalized, out var currency) && currency.IsActive ? currency : null;
    }

    public Currency AddOrActivate(string code, string name, int? minorUnit)
    {
        var currency = new Currency(code.Trim().ToUpperInvariant(), name.Trim(), minorUnit, true);
        store.Currencies[currency.Code] = currency;
        return currency;
    }
}

public sealed class InMemoryReportingPeriodRepository(ExpenseStore store) : IReportingPeriodRepository
{
    public IReadOnlyCollection<ReportingPeriod> GetByUser(long userId) =>
        store.ReportingPeriods.Values
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.StartDate)
            .ToList();

    public ReportingPeriod? GetActive(long userId) =>
        store.ReportingPeriods.Values
            .Where(x => x.UserId == userId && !x.IsClosed)
            .OrderByDescending(x => x.StartDate)
            .FirstOrDefault();

    public ReportingPeriod? GetById(long userId, long id) =>
        store.ReportingPeriods.Values.FirstOrDefault(x => x.Id == id && x.UserId == userId);

    public ReportingPeriod Add(long userId, DateOnly startDate, DateOnly endDate)
    {
        var period = new ReportingPeriod(store.NextReportingPeriodId(), userId, startDate, endDate, false, null, DateTime.UtcNow);
        store.ReportingPeriods[period.Id] = period;
        return period;
    }

    public ReportingPeriod Update(ReportingPeriod period)
    {
        store.ReportingPeriods[period.Id] = period;
        return period;
    }
}
