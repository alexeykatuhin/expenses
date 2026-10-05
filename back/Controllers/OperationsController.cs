using FamilyExpenses.Api.Models;
using FamilyExpenses.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FamilyExpenses.Api.Controllers;

[ApiController]
public sealed class OperationsController(IOperationService operations) : ControllerBase
{
    [HttpGet("api/v1/incomes")]
    public IActionResult ListIncomes(
        [FromQuery(Name = "date_from")] DateOnly? dateFrom,
        [FromQuery(Name = "date_to")] DateOnly? dateTo,
        [FromQuery(Name = "category_id")] long? categoryId,
        [FromQuery(Name = "subcategory_id")] long? subcategoryId,
        [FromQuery] int? limit,
        [FromQuery] int? offset) =>
        List(EntryKind.Income, dateFrom, dateTo, categoryId, subcategoryId, limit, offset);

    [HttpGet("api/v1/expenses")]
    public IActionResult ListExpenses(
        [FromQuery(Name = "date_from")] DateOnly? dateFrom,
        [FromQuery(Name = "date_to")] DateOnly? dateTo,
        [FromQuery(Name = "category_id")] long? categoryId,
        [FromQuery(Name = "subcategory_id")] long? subcategoryId,
        [FromQuery] int? limit,
        [FromQuery] int? offset) =>
        List(EntryKind.Expense, dateFrom, dateTo, categoryId, subcategoryId, limit, offset);

    [HttpGet("api/v1/incomes/{id:long}")]
    public IActionResult GetIncome(long id) => Get(EntryKind.Income, id);

    [HttpGet("api/v1/expenses/{id:long}")]
    public IActionResult GetExpense(long id) => Get(EntryKind.Expense, id);

    [HttpPost("api/v1/incomes")]
    public Task<IActionResult> CreateIncome(CreateOperationRequest request, CancellationToken ct) => Create(EntryKind.Income, "incomes", request, ct);

    [HttpPost("api/v1/expenses")]
    public Task<IActionResult> CreateExpense(CreateOperationRequest request, CancellationToken ct) => Create(EntryKind.Expense, "expenses", request, ct);

    [HttpPatch("api/v1/incomes/{id:long}")]
    public Task<IActionResult> UpdateIncome(long id, UpdateOperationRequest request, CancellationToken ct) => Update(EntryKind.Income, id, request, ct);

    [HttpPatch("api/v1/expenses/{id:long}")]
    public Task<IActionResult> UpdateExpense(long id, UpdateOperationRequest request, CancellationToken ct) => Update(EntryKind.Expense, id, request, ct);

    [HttpDelete("api/v1/incomes/{id:long}")]
    public IActionResult DeleteIncome(long id) => Delete(EntryKind.Income, id);

    [HttpDelete("api/v1/expenses/{id:long}")]
    public IActionResult DeleteExpense(long id) => Delete(EntryKind.Expense, id);

    [HttpGet("api/v1/operations")]
    public IActionResult Feed(
        [FromQuery(Name = "type")] string? type,
        [FromQuery(Name = "date_from")] DateOnly? dateFrom,
        [FromQuery(Name = "date_to")] DateOnly? dateTo,
        [FromQuery(Name = "category_id")] long? categoryId,
        [FromQuery(Name = "subcategory_id")] long? subcategoryId,
        [FromQuery] int? limit,
        [FromQuery] int? offset)
    {
        var result = operations.Feed(type, dateFrom, dateTo, categoryId, subcategoryId, limit, offset);
        if (result.Unauthorized)
        {
            return Unauthorized();
        }

        return result.Error is not null ? BadRequest(result.Error) : Ok(result.Response);
    }

    private IActionResult List(EntryKind kind, DateOnly? dateFrom, DateOnly? dateTo, long? categoryId, long? subcategoryId, int? limit, int? offset)
    {
        var response = operations.List(kind, dateFrom, dateTo, categoryId, subcategoryId, limit, offset);
        return response is null ? Unauthorized() : Ok(response);
    }

    private IActionResult Get(EntryKind kind, long id)
    {
        if (!IsAuthenticated())
        {
            return Unauthorized();
        }

        var response = operations.Get(kind, id);
        return response is null ? NotFound() : Ok(response);
    }

    private async Task<IActionResult> Create(EntryKind kind, string route, CreateOperationRequest request, CancellationToken ct)
    {
        var result = await operations.Create(kind, request, ct);
        if (result.Unauthorized)
        {
            return Unauthorized();
        }

        if (result.Error is not null)
        {
            return StatusCode(result.StatusCode ?? StatusCodes.Status400BadRequest, result.Error);
        }

        return Created($"/api/v1/{route}/{result.Response!.Id}", result.Response);
    }

    private async Task<IActionResult> Update(EntryKind kind, long id, UpdateOperationRequest request, CancellationToken ct)
    {
        var result = await operations.Update(kind, id, request, ct);
        if (result.Unauthorized)
        {
            return Unauthorized();
        }

        if (result.NotFound)
        {
            return NotFound();
        }

        return result.Error is not null ? StatusCode(result.StatusCode ?? StatusCodes.Status400BadRequest, result.Error) : Ok(result.Response);
    }

    private IActionResult Delete(EntryKind kind, long id)
    {
        operations.Delete(kind, id, out var unauthorized, out var notFound, out var error);
        if (unauthorized)
        {
            return Unauthorized();
        }

        if (notFound)
        {
            return NotFound();
        }

        return error is not null ? BadRequest(error) : NoContent();
    }

    private bool IsAuthenticated() => User.Identity?.IsAuthenticated == true;
}
