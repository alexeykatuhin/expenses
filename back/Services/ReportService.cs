using FamilyExpenses.Api.Models;
using FamilyExpenses.Api.Repositories;
using static FamilyExpenses.Api.Models.ResponseMapper;
using static FamilyExpenses.Api.Services.ServiceHelpers;

namespace FamilyExpenses.Api.Services;

public interface IReportService
{
    SummaryResponse? Summary(DateOnly? dateFrom, DateOnly? dateTo, long? periodId = null);
    (IReadOnlyCollection<GroupReportResponse>? Response, ApiError? Error, bool Unauthorized) Grouped(string type, DateOnly? dateFrom, DateOnly? dateTo, bool bySubcategory, long? periodId = null);
    (IReadOnlyCollection<PeriodCategoryReportResponse>? Response, ApiError? Error, bool Unauthorized) PeriodCategory(string type, DateOnly? dateFrom, DateOnly? dateTo);
}

public sealed class ReportService(
    ICurrentUserService currentUser,
    IOperationRepository operations,
    IReportingPeriodRepository periods) : IReportService
{
    public SummaryResponse? Summary(DateOnly? dateFrom, DateOnly? dateTo, long? periodId = null)
    {
        if (!currentUser.TryGetUserId(out var userId))
        {
            return null;
        }

        (dateFrom, dateTo) = IgnoreDateRangeForPeriod(dateFrom, dateTo, periodId);
        var rows = operations.GetByUser(userId, dateFrom: dateFrom, dateTo: dateTo, periodId: periodId);
        var incomeRub = rows.Where(x => x.Kind == EntryKind.Income).Sum(x => x.AmountRub);
        var incomeUsd = rows.Where(x => x.Kind == EntryKind.Income).Sum(x => x.AmountUsd);
        var expenseRub = rows.Where(x => x.Kind == EntryKind.Expense).Sum(x => x.AmountRub);
        var expenseUsd = rows.Where(x => x.Kind == EntryKind.Expense).Sum(x => x.AmountUsd);

        return new SummaryResponse(
            new MoneyResponse(incomeRub, incomeUsd),
            new MoneyResponse(expenseRub, expenseUsd),
            new MoneyResponse(incomeRub - expenseRub, incomeUsd - expenseUsd));
    }

    public (IReadOnlyCollection<GroupReportResponse>? Response, ApiError? Error, bool Unauthorized) Grouped(string type, DateOnly? dateFrom, DateOnly? dateTo, bool bySubcategory, long? periodId = null)
    {
        if (!currentUser.TryGetUserId(out var userId))
        {
            return (null, null, true);
        }

        var kind = ParseKind(type);
        if (kind is null)
        {
            return (null, ApiError.Validation([new ErrorDetail("type", "Allowed values are income and expense")]), false);
        }

        (dateFrom, dateTo) = IgnoreDateRangeForPeriod(dateFrom, dateTo, periodId);
        var rows = operations.GetByUser(userId, kind, dateFrom, dateTo, periodId: periodId)
            .GroupBy(x => bySubcategory ? x.SubcategoryId : x.CategoryId)
            .Select(x => new { Id = x.Key, Rub = x.Sum(y => y.AmountRub), Usd = x.Sum(y => y.AmountUsd) })
            .OrderByDescending(x => x.Rub)
            .Select(x => ToGroupReport(x.Id, x.Rub, x.Usd))
            .ToList();

        return (rows, null, false);
    }

    public (IReadOnlyCollection<PeriodCategoryReportResponse>? Response, ApiError? Error, bool Unauthorized) PeriodCategory(string type, DateOnly? dateFrom, DateOnly? dateTo)
    {
        if (!currentUser.TryGetUserId(out var userId))
        {
            return (null, null, true);
        }

        var kind = ParseKind(type);
        if (kind is null)
        {
            return (null, ApiError.Validation([new ErrorDetail("type", "Allowed values are income and expense")]), false);
        }

        var periodsById = periods.GetByUser(userId)
            .Where(x => IsPeriodInRange(x, dateFrom, dateTo))
            .ToDictionary(x => x.Id);

        var rows = operations.GetByUser(userId, kind)
            .Where(x => x.PeriodId is not null && periodsById.ContainsKey(x.PeriodId.Value))
            .GroupBy(x =>
            {
                var period = periodsById[x.PeriodId!.Value];
                return new { PeriodStart = period.StartDate, period.EndDate, x.CategoryId };
            })
            .Select(x => new
            {
                x.Key.PeriodStart,
                PeriodEnd = x.Key.EndDate,
                Id = (long?)x.Key.CategoryId,
                Rub = x.Sum(y => y.AmountRub),
                Usd = x.Sum(y => y.AmountUsd)
            })
            .OrderBy(x => x.PeriodStart)
            .ThenByDescending(x => x.Rub)
            .Select(x => ToPeriodCategoryReport(x.PeriodStart, x.PeriodEnd, x.Id, x.Rub, x.Usd))
            .ToList();

        return (rows, null, false);
    }

    private static (DateOnly? DateFrom, DateOnly? DateTo) IgnoreDateRangeForPeriod(DateOnly? dateFrom, DateOnly? dateTo, long? periodId) =>
        periodId is null ? (dateFrom, dateTo) : (null, null);

    private static bool IsPeriodInRange(ReportingPeriod period, DateOnly? dateFrom, DateOnly? dateTo) =>
        (dateFrom is null || period.StartDate >= dateFrom) &&
        (dateTo is null || period.EndDate <= dateTo);
}
