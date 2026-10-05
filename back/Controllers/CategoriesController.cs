using FamilyExpenses.Api.Models;
using FamilyExpenses.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FamilyExpenses.Api.Controllers;

[ApiController]
public sealed class CategoriesController(ICategoryService categories) : ControllerBase
{
    [HttpGet("api/v1/income-categories")]
    public IActionResult ListIncome([FromQuery(Name = "is_active")] bool? isActive) => List(EntryKind.Income, isActive);

    [HttpGet("api/v1/expense-categories")]
    public IActionResult ListExpenses([FromQuery(Name = "is_active")] bool? isActive) => List(EntryKind.Expense, isActive);

    [HttpGet("api/v1/income-categories/{id:long}")]
    public IActionResult GetIncome(long id) => Get(EntryKind.Income, id);

    [HttpGet("api/v1/expense-categories/{id:long}")]
    public IActionResult GetExpense(long id) => Get(EntryKind.Expense, id);

    [HttpPost("api/v1/income-categories")]
    public IActionResult CreateIncome(CreateCategoryRequest request) => Create(EntryKind.Income, "income-categories", request);

    [HttpPost("api/v1/expense-categories")]
    public IActionResult CreateExpense(CreateCategoryRequest request) => Create(EntryKind.Expense, "expense-categories", request);

    [HttpPatch("api/v1/income-categories/{id:long}")]
    public IActionResult UpdateIncome(long id, UpdateCategoryRequest request) => Update(EntryKind.Income, id, request);

    [HttpPatch("api/v1/expense-categories/{id:long}")]
    public IActionResult UpdateExpense(long id, UpdateCategoryRequest request) => Update(EntryKind.Expense, id, request);

    [HttpDelete("api/v1/income-categories/{id:long}")]
    public IActionResult DeleteIncome(long id) => Delete(EntryKind.Income, id);

    [HttpDelete("api/v1/expense-categories/{id:long}")]
    public IActionResult DeleteExpense(long id) => Delete(EntryKind.Expense, id);

    private IActionResult List(EntryKind kind, bool? isActive)
    {
        var response = categories.List(kind, isActive);
        return response is null ? Unauthorized() : Ok(response);
    }

    private IActionResult Get(EntryKind kind, long id)
    {
        if (!IsAuthenticated())
        {
            return Unauthorized();
        }

        var response = categories.Get(kind, id);
        return response is null ? NotFound() : Ok(response);
    }

    private IActionResult Create(EntryKind kind, string route, CreateCategoryRequest request)
    {
        var result = categories.Create(kind, request);
        if (result.Unauthorized)
        {
            return Unauthorized();
        }

        if (result.Error is not null)
        {
            return BadRequest(result.Error);
        }

        return Created($"/api/v1/{route}/{result.Response!.Id}", result.Response);
    }

    private IActionResult Update(EntryKind kind, long id, UpdateCategoryRequest request)
    {
        var result = categories.Update(kind, id, request);
        if (result.Unauthorized)
        {
            return Unauthorized();
        }

        if (result.NotFound)
        {
            return NotFound();
        }

        return result.Error is not null ? BadRequest(result.Error) : Ok(result.Response);
    }

    private IActionResult Delete(EntryKind kind, long id)
    {
        categories.Deactivate(kind, id, out var unauthorized, out var notFound);
        if (unauthorized)
        {
            return Unauthorized();
        }

        return notFound ? NotFound() : NoContent();
    }

    private bool IsAuthenticated() => User.Identity?.IsAuthenticated == true;
}
