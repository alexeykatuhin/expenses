using FamilyExpenses.Api.Models;
using FamilyExpenses.Api.Repositories;
using static FamilyExpenses.Api.Models.ResponseMapper;
using static FamilyExpenses.Api.Services.ServiceHelpers;

namespace FamilyExpenses.Api.Services;

public interface IOperationService
{
    PageResponse<OperationResponse>? List(EntryKind kind, DateOnly? dateFrom, DateOnly? dateTo, long? categoryId, long? subcategoryId, int? limit, int? offset);
    OperationResponse? Get(EntryKind kind, long id);
    Task<(OperationResponse? Response, ApiError? Error, int? StatusCode, bool Unauthorized)> Create(EntryKind kind, CreateOperationRequest request, CancellationToken ct);
    Task<(OperationResponse? Response, ApiError? Error, int? StatusCode, bool Unauthorized, bool NotFound)> Update(EntryKind kind, long id, UpdateOperationRequest request, CancellationToken ct);
    bool Delete(EntryKind kind, long id, out bool unauthorized, out bool notFound, out ApiError? error);
    (PageResponse<OperationFeedResponse>? Response, ApiError? Error, bool Unauthorized) Feed(string? type, DateOnly? dateFrom, DateOnly? dateTo, long? categoryId, long? subcategoryId, int? limit, int? offset);
}

public sealed class OperationService(
    ICurrentUserService currentUser,
    ICategoryRepository categories,
    ISubcategoryRepository subcategories,
    IOperationRepository operations,
    ICurrencyRepository currencies,
    IExchangeRateService exchangeRates,
    IReportingPeriodService periods) : IOperationService
{
    public PageResponse<OperationResponse>? List(EntryKind kind, DateOnly? dateFrom, DateOnly? dateTo, long? categoryId, long? subcategoryId, int? limit, int? offset)
    {
        if (!currentUser.TryGetUserId(out var userId))
        {
            return null;
        }

        var all = operations.GetByUser(userId, kind, dateFrom, dateTo, categoryId, subcategoryId);
        var page = Page(all, limit, offset);
        return new PageResponse<OperationResponse>(page.Items.Select(ToOperationResponse).ToList(), page.Limit, page.Offset, all.Count);
    }

    public OperationResponse? Get(EntryKind kind, long id)
    {
        if (!currentUser.TryGetUserId(out var userId))
        {
            return null;
        }

        var operation = operations.GetById(userId, kind, id);
        return operation is null ? null : ToOperationResponse(operation);
    }

    public async Task<(OperationResponse? Response, ApiError? Error, int? StatusCode, bool Unauthorized)> Create(EntryKind kind, CreateOperationRequest request, CancellationToken ct)
    {
        if (!currentUser.TryGetUserId(out var userId))
        {
            return (null, null, null, true);
        }

        var activePeriod = periods.EnsureActive(userId);
        var currencyCode = NormalizeCode(request.CurrencyCode);
        var errors = ValidateReferences(userId, kind, request.CategoryId, request.SubcategoryId, request.OriginalAmount, currencyCode);

        if (errors.Count > 0)
        {
            return (null, ApiError.Validation(errors), StatusCodes.Status400BadRequest, false);
        }

        var converted = await ConvertOrError(request.OriginalAmount, currencyCode, ct);
        if (converted.Error is not null)
        {
            return (null, converted.Error, converted.StatusCode, false);
        }

        var operationDate = DateOnly.FromDateTime(DateTime.Now);
        var operation = operations.Add(userId, kind, request.CategoryId, request.SubcategoryId, activePeriod.Id, request.OriginalAmount, currencyCode, converted.Value!, operationDate, request.Comment);
        return (ToOperationResponse(operation), null, null, false);
    }

    public async Task<(OperationResponse? Response, ApiError? Error, int? StatusCode, bool Unauthorized, bool NotFound)> Update(EntryKind kind, long id, UpdateOperationRequest request, CancellationToken ct)
    {
        if (!currentUser.TryGetUserId(out var userId))
        {
            return (null, null, null, true, false);
        }

        var operation = operations.GetById(userId, kind, id);
        if (operation is null)
        {
            return (null, null, null, false, true);
        }

        if (IsClosedPeriod(userId, operation))
        {
            return (null, ApiError.Validation([new ErrorDetail("period_id", "Reporting period is closed")]), StatusCodes.Status400BadRequest, false, false);
        }

        var targetCategoryId = request.CategoryId ?? operation.CategoryId;
        var targetSubcategoryId = request.SubcategoryId ?? (kind == EntryKind.Expense && request.CategoryId.HasValue ? null : operation.SubcategoryId);
        var targetOriginalAmount = request.OriginalAmount ?? operation.OriginalAmount;
        var targetCurrencyCode = NormalizeCode(request.CurrencyCode ?? operation.CurrencyCode);
        var errors = ValidateReferences(userId, kind, targetCategoryId, targetSubcategoryId, targetOriginalAmount, targetCurrencyCode);

        if (errors.Count > 0)
        {
            return (null, ApiError.Validation(errors), StatusCodes.Status400BadRequest, false, false);
        }

        operation.CategoryId = targetCategoryId;
        operation.SubcategoryId = targetSubcategoryId;
        var shouldRecalculate = targetOriginalAmount != operation.OriginalAmount || targetCurrencyCode != operation.CurrencyCode;
        if (shouldRecalculate)
        {
            var converted = await ConvertOrError(targetOriginalAmount, targetCurrencyCode, ct);
            if (converted.Error is not null)
            {
                return (null, converted.Error, converted.StatusCode, false, false);
            }

            operation.OriginalAmount = targetOriginalAmount;
            operation.CurrencyCode = targetCurrencyCode;
            operation.AmountRub = converted.Value!.AmountRub;
            operation.AmountUsd = converted.Value.AmountUsd;
            operation.ExchangeRateDate = converted.Value.ExchangeRateDate;
            operation.RubRate = converted.Value.RubRate;
            operation.UsdRate = converted.Value.UsdRate;
        }

        if (request.Comment is not null)
        {
            operation.Comment = request.Comment;
        }

        operation.UpdatedAt = DateTime.UtcNow;
        operation = operations.Update(operation);
        return (ToOperationResponse(operation), null, null, false, false);
    }

    public bool Delete(EntryKind kind, long id, out bool unauthorized, out bool notFound, out ApiError? error)
    {
        unauthorized = false;
        notFound = false;
        error = null;

        if (!currentUser.TryGetUserId(out var userId))
        {
            unauthorized = true;
            return false;
        }

        var operation = operations.GetById(userId, kind, id);
        if (operation is null)
        {
            notFound = true;
            return false;
        }

        if (IsClosedPeriod(userId, operation))
        {
            error = ApiError.Validation([new ErrorDetail("period_id", "Reporting period is closed")]);
            return false;
        }

        return operations.Remove(operation.Id);
    }

    public (PageResponse<OperationFeedResponse>? Response, ApiError? Error, bool Unauthorized) Feed(string? type, DateOnly? dateFrom, DateOnly? dateTo, long? categoryId, long? subcategoryId, int? limit, int? offset)
    {
        if (!currentUser.TryGetUserId(out var userId))
        {
            return (null, null, true);
        }

        var kind = ParseKind(type);
        if (type is not null && kind is null)
        {
            return (null, ApiError.Validation([new ErrorDetail("type", "Allowed values are income and expense")]), false);
        }

        var all = operations.GetByUser(userId, kind, dateFrom, dateTo, categoryId, subcategoryId);
        var page = Page(all, limit, offset);
        return (new PageResponse<OperationFeedResponse>(page.Items.Select(ToOperationFeed).ToList(), page.Limit, page.Offset, all.Count), null, false);
    }

    private List<ErrorDetail> ValidateReferences(long userId, EntryKind kind, long categoryId, long? subcategoryId, decimal originalAmount, string currencyCode)
    {
        var errors = new List<ErrorDetail>();

        if (categories.GetById(userId, kind, categoryId) is null)
        {
            errors.Add(new ErrorDetail("category_id", "Category was not found"));
        }

        if (subcategoryId is not null)
        {
            var subcategory = subcategories.GetById(userId, kind, subcategoryId.Value);
            if (subcategory is null)
            {
                errors.Add(new ErrorDetail("subcategory_id", "Subcategory was not found"));
            }
            else if (subcategory.CategoryId != categoryId)
            {
                errors.Add(new ErrorDetail("subcategory_id", "Subcategory must belong to selected category"));
            }
        }

        if (originalAmount < 0)
        {
            errors.Add(new ErrorDetail("original_amount", "Value must be greater than or equal to 0"));
        }

        if (currencies.GetActiveByCode(currencyCode) is null)
        {
            errors.Add(new ErrorDetail("currency_code", "Currency code is invalid or inactive"));
        }

        return errors;
    }

    private async Task<(ConvertedMoney? Value, ApiError? Error, int? StatusCode)> ConvertOrError(decimal amount, string currencyCode, CancellationToken ct)
    {
        try
        {
            return (await exchangeRates.ConvertToRubAndUsdAsync(amount, currencyCode, ct), null, null);
        }
        catch (CurrencyRateNotAvailableException)
        {
            return (null, new ApiError(new ErrorBody(
                "CURRENCY_RATE_NOT_AVAILABLE",
                "Currency rate is not available",
                [new ErrorDetail("currency_code", "Rate to RUB or USD is not available")])), StatusCodes.Status422UnprocessableEntity);
        }
        catch (ExchangeRateUnavailableException)
        {
            return (null, new ApiError(new ErrorBody(
                "EXCHANGE_RATE_PROVIDER_UNAVAILABLE",
                "Exchange rate provider is unavailable",
                [])), StatusCodes.Status503ServiceUnavailable);
        }
    }

    private bool IsClosedPeriod(long userId, Operation operation)
    {
        if (operation.PeriodId is null)
        {
            return false;
        }

        var activePeriod = periods.EnsureActive(userId);
        return operation.PeriodId != activePeriod.Id;
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
}
