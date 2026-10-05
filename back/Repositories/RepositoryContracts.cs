using FamilyExpenses.Api.Models;

namespace FamilyExpenses.Api.Repositories;

public interface IUserRepository
{
    IReadOnlyCollection<User> GetAll();
    User? GetById(long id);
    User? GetActiveByEmail(string email);
    bool EmailExists(string email, long? exceptId = null);
    User Add(string email, string passwordHash, string? displayName);
    User Update(User user);
}

public interface IRefreshTokenRepository
{
    RefreshToken? GetValidByHash(string tokenHash);
    RefreshToken? GetByUserAndHash(long userId, string tokenHash);
    RefreshToken Add(long userId, string tokenHash, string? userAgent, string? ipAddress, DateTime expiresAt);
    void Revoke(long id, DateTime revokedAt);
}

public interface ICategoryRepository
{
    IReadOnlyCollection<Category> GetByUser(long userId, EntryKind kind, bool? isActive = null);
    Category? GetById(long userId, EntryKind kind, long id);
    bool NameExists(long userId, EntryKind kind, string name, long? exceptId = null);
    Category Add(long userId, EntryKind kind, string name, bool isActive);
    Category Update(Category category);
}

public interface ISubcategoryRepository
{
    IReadOnlyCollection<Subcategory> GetByUser(long userId, EntryKind kind, long? categoryId = null, bool? isActive = null);
    Subcategory? GetById(long userId, EntryKind kind, long id);
    bool NameExists(long userId, EntryKind kind, long categoryId, string name, long? exceptId = null);
    Subcategory Add(long userId, EntryKind kind, long categoryId, string name, bool isActive);
    Subcategory Update(Subcategory subcategory);
}

public interface IOperationRepository
{
    IReadOnlyCollection<Operation> GetByUser(long userId, EntryKind? kind = null, DateOnly? dateFrom = null, DateOnly? dateTo = null, long? categoryId = null, long? subcategoryId = null, long? periodId = null);
    Operation? GetById(long userId, EntryKind kind, long id);
    Operation Add(long userId, EntryKind kind, long categoryId, long? subcategoryId, long periodId, decimal originalAmount, string currencyCode, ConvertedMoney converted, DateOnly operationDate, string? comment);
    Operation Update(Operation operation);
    bool Remove(long id);
}

public interface ICurrencyRepository
{
    IReadOnlyCollection<CurrencyUsage> GetActiveWithLastUsage(long userId);
    Currency? GetActiveByCode(string code);
    Currency AddOrActivate(string code, string name, int? minorUnit);
}

public interface IReportingPeriodRepository
{
    IReadOnlyCollection<ReportingPeriod> GetByUser(long userId);
    ReportingPeriod? GetActive(long userId);
    ReportingPeriod? GetById(long userId, long id);
    ReportingPeriod Add(long userId, DateOnly startDate, DateOnly endDate);
    ReportingPeriod Update(ReportingPeriod period);
}
