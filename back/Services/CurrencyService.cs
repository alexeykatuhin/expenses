using System.Text.Json;
using FamilyExpenses.Api.Models;
using FamilyExpenses.Api.Repositories;
using static FamilyExpenses.Api.Models.ResponseMapper;

namespace FamilyExpenses.Api.Services;

public interface ICurrencyService
{
    IReadOnlyCollection<CurrencyResponse>? List();
    (CurrencyResponse? Response, ApiError? Error, bool Unauthorized) Create(CreateCurrencyRequest request);
}

public sealed class CurrencyService(ICurrentUserService currentUser, ICurrencyRepository currencies) : ICurrencyService
{
    public IReadOnlyCollection<CurrencyResponse>? List()
    {
        if (!currentUser.TryGetUserId(out var userId))
        {
            return null;
        }

        return currencies.GetActiveWithLastUsage(userId)
            .Select(ToCurrencyResponse)
            .ToList();
    }

    public (CurrencyResponse? Response, ApiError? Error, bool Unauthorized) Create(CreateCurrencyRequest request)
    {
        if (!currentUser.TryGetUserId(out _))
        {
            return (null, null, true);
        }

        var code = request.Code.Trim().ToUpperInvariant();
        var errors = new List<ErrorDetail>();

        if (code.Length != 3 || code.Any(x => x < 'A' || x > 'Z'))
        {
            errors.Add(new ErrorDetail("code", "Currency code must contain three Latin letters"));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add(new ErrorDetail("name", "Currency name is required"));
        }

        if (request.MinorUnit is < 0 or > 8)
        {
            errors.Add(new ErrorDetail("minor_unit", "Minor unit must be from 0 to 8"));
        }

        if (errors.Count > 0)
        {
            return (null, ApiError.Validation(errors), false);
        }

        var currency = currencies.AddOrActivate(code, request.Name, request.MinorUnit);
        return (new CurrencyResponse(currency.Code, currency.Name, currency.MinorUnit, null), null, false);
    }
}

public sealed class ExchangeRateUnavailableException : Exception;
public sealed class CurrencyRateNotAvailableException(string currencyCode) : Exception
{
    public string CurrencyCode { get; } = currencyCode;
}

public sealed class ExchangeRates(string baseCurrencyCode, DateOnly date, IReadOnlyDictionary<string, decimal> rates)
{
    public string BaseCurrencyCode { get; } = baseCurrencyCode;
    public DateOnly Date { get; } = date;
    public IReadOnlyDictionary<string, decimal> Rates { get; } = rates;
}

public interface IExchangeRateProvider
{
    Task<ExchangeRates> GetLatestRatesAsync(string baseCurrencyCode, CancellationToken ct);
}

public sealed class ExchangeRateApiProvider(HttpClient httpClient) : IExchangeRateProvider
{
    public async Task<ExchangeRates> GetLatestRatesAsync(string baseCurrencyCode, CancellationToken ct)
    {
        var code = NormalizeCode(baseCurrencyCode);
        HttpResponseMessage response;

        try
        {
            response = await httpClient.GetAsync($"https://open.er-api.com/v6/latest/{code}", ct);
        }
        catch (HttpRequestException)
        {
            throw new ExchangeRateUnavailableException();
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ExchangeRateUnavailableException();
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new ExchangeRateUnavailableException();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = document.RootElement;

        if (!root.TryGetProperty("result", out var result) || !string.Equals(result.GetString(), "success", StringComparison.OrdinalIgnoreCase))
        {
            throw new CurrencyRateNotAvailableException(code);
        }

        if (!root.TryGetProperty("rates", out var ratesElement) || ratesElement.ValueKind != JsonValueKind.Object)
        {
            throw new CurrencyRateNotAvailableException(code);
        }

        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in ratesElement.EnumerateObject())
        {
            if (property.Value.TryGetDecimal(out var rate))
            {
                rates[property.Name.ToUpperInvariant()] = rate;
            }
        }

        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        if (root.TryGetProperty("time_last_update_unix", out var updated) && updated.TryGetInt64(out var unix))
        {
            date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime);
        }

        return new ExchangeRates(code, date, rates);
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
}

public interface IExchangeRateService
{
    Task<ConvertedMoney> ConvertToRubAndUsdAsync(decimal amount, string sourceCurrencyCode, CancellationToken ct);
}

public sealed class ExchangeRateService(ICurrencyRepository currencies, IExchangeRateProvider provider) : IExchangeRateService
{
    public async Task<ConvertedMoney> ConvertToRubAndUsdAsync(decimal amount, string sourceCurrencyCode, CancellationToken ct)
    {
        var code = NormalizeCode(sourceCurrencyCode);
        if (currencies.GetActiveByCode(code) is null)
        {
            throw new CurrencyRateNotAvailableException(code);
        }

        var rates = await provider.GetLatestRatesAsync(code, ct);
        var rubRate = code == "RUB" ? 1m : GetRate(rates, code, "RUB");
        var usdRate = code == "USD" ? 1m : GetRate(rates, code, "USD");

        return new ConvertedMoney(
            Math.Round(amount * rubRate, 2, MidpointRounding.AwayFromZero),
            Math.Round(amount * usdRate, 2, MidpointRounding.AwayFromZero),
            rates.Date,
            rubRate,
            usdRate);
    }

    private static decimal GetRate(ExchangeRates rates, string sourceCurrencyCode, string targetCurrencyCode)
    {
        if (rates.Rates.TryGetValue(targetCurrencyCode, out var rate))
        {
            return rate;
        }

        throw new CurrencyRateNotAvailableException(sourceCurrencyCode);
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
}
