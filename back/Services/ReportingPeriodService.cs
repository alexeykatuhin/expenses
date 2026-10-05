using FamilyExpenses.Api.Models;
using FamilyExpenses.Api.Repositories;
using static FamilyExpenses.Api.Models.ResponseMapper;

namespace FamilyExpenses.Api.Services;

public interface IReportingPeriodService
{
    IReadOnlyCollection<ReportingPeriodResponse>? List();
    ReportingPeriodResponse? GetActive();
    (ReportingPeriodResponse? Response, bool Unauthorized, bool NotFound, bool AlreadyClosed, bool EndsInFuture) Close(long id);
    ReportingPeriod EnsureActive(long userId);
}

public sealed class ReportingPeriodService(
    ICurrentUserService currentUser,
    IReportingPeriodRepository periods) : IReportingPeriodService
{
    public IReadOnlyCollection<ReportingPeriodResponse>? List()
    {
        if (!currentUser.TryGetUserId(out var userId))
        {
            return null;
        }

        return periods.GetByUser(userId).Select(ToReportingPeriodResponse).ToList();
    }

    public ReportingPeriodResponse? GetActive()
    {
        if (!currentUser.TryGetUserId(out var userId))
        {
            return null;
        }

        return ToReportingPeriodResponse(EnsureActive(userId));
    }

    public (ReportingPeriodResponse? Response, bool Unauthorized, bool NotFound, bool AlreadyClosed, bool EndsInFuture) Close(long id)
    {
        if (!currentUser.TryGetUserId(out var userId))
        {
            return (null, true, false, false, false);
        }

        var period = periods.GetById(userId, id);
        if (period is null)
        {
            return (null, false, true, false, false);
        }

        if (period.IsClosed)
        {
            return (null, false, false, true, false);
        }

        if (DateOnly.FromDateTime(DateTime.Now) <= period.EndDate)
        {
            return (null, false, false, false, true);
        }

        period.IsClosed = true;
        period.ClosedAt = DateTime.UtcNow;
        periods.Update(period);

        var nextStart = period.EndDate.AddDays(1);
        var nextEnd = nextStart.AddMonths(1).AddDays(-1);
        var nextPeriod = periods.GetActive(userId) ?? periods.Add(userId, nextStart, nextEnd);

        return (ToReportingPeriodResponse(nextPeriod), false, false, false, false);
    }

    public ReportingPeriod EnsureActive(long userId)
    {
        var active = periods.GetActive(userId);
        if (active is not null)
        {
            return active;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var currentStart = GetPeriodStart(today);
        return periods.Add(userId, currentStart, currentStart.AddMonths(1).AddDays(-1));
    }

    private static DateOnly GetPeriodStart(DateOnly date) =>
        date.Day >= 15
            ? new DateOnly(date.Year, date.Month, 15)
            : new DateOnly(date.AddMonths(-1).Year, date.AddMonths(-1).Month, 15);
}
