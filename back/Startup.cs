using System.Text.Json;
using FamilyExpenses.Api.Models;
using FamilyExpenses.Api.Repositories;
using FamilyExpenses.Api.Security;
using FamilyExpenses.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace FamilyExpenses.Api;

public sealed class Startup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var details = context.ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .SelectMany(x => x.Value!.Errors.Select(error =>
                        new ErrorDetail(JsonNamingPolicy.SnakeCaseLower.ConvertName(x.Key), error.ErrorMessage)))
                    .ToList();

                return new BadRequestObjectResult(ApiError.Validation(details));
            };
        });

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        services.AddHostedService<PostgresDatabaseInitializer>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IUserRepository, PostgresUserRepository>();
        services.AddSingleton<IRefreshTokenRepository, PostgresRefreshTokenRepository>();
        services.AddSingleton<ICategoryRepository, PostgresCategoryRepository>();
        services.AddSingleton<ISubcategoryRepository, PostgresSubcategoryRepository>();
        services.AddSingleton<IOperationRepository, PostgresOperationRepository>();
        services.AddSingleton<ICurrencyRepository, PostgresCurrencyRepository>();
        services.AddSingleton<IReportingPeriodRepository, PostgresReportingPeriodRepository>();
        services.AddHttpClient<IExchangeRateProvider, ExchangeRateApiProvider>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ISubcategoryService, SubcategoryService>();
        services.AddScoped<IReportingPeriodService, ReportingPeriodService>();
        services.AddScoped<IOperationService, OperationService>();
        services.AddScoped<ICurrencyService, CurrencyService>();
        services.AddScoped<IExchangeRateService, ExchangeRateService>();
        services.AddScoped<IReportService, ReportService>();
    }

    public void Configure(WebApplication app, IWebHostEnvironment environment)
    {
        app.UseMiddleware<AccessTokenMiddleware>();
        app.MapControllers();
    }
}
