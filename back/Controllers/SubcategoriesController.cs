using FamilyExpenses.Api.Models;
using FamilyExpenses.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FamilyExpenses.Api.Controllers;

[ApiController]
public sealed class SubcategoriesController(ISubcategoryService subcategories) : ControllerBase
{
    [HttpGet("api/v1/income-subcategories")]
    public IActionResult ListIncome([FromQuery(Name = "category_id")] long? categoryId, [FromQuery(Name = "is_active")] bool? isActive) =>
        List(EntryKind.Income, categoryId, isActive);

    [HttpGet("api/v1/expense-subcategories")]
    public IActionResult ListExpense([FromQuery(Name = "category_id")] long? categoryId, [FromQuery(Name = "is_active")] bool? isActive) =>
        List(EntryKind.Expense, categoryId, isActive);

    [HttpGet("api/v1/income-subcategories/{id:long}")]
    public IActionResult GetIncome(long id) => Get(EntryKind.Income, id);

    [HttpGet("api/v1/expense-subcategories/{id:long}")]
    public IActionResult GetExpense(long id) => Get(EntryKind.Expense, id);

    [HttpPost("api/v1/income-subcategories")]
    public IActionResult CreateIncome(CreateSubcategoryRequest request) => Create(EntryKind.Income, "income-subcategories", request);

    [HttpPost("api/v1/expense-subcategories")]
    public IActionResult CreateExpense(CreateSubcategoryRequest request) => Create(EntryKind.Expense, "expense-subcategories", request);

    [HttpPatch("api/v1/income-subcategories/{id:long}")]
    public IActionResult UpdateIncome(long id, UpdateSubcategoryRequest request) => Update(EntryKind.Income, id, request);

    [HttpPatch("api/v1/expense-subcategories/{id:long}")]
    public IActionResult UpdateExpense(long id, UpdateSubcategoryRequest request) => Update(EntryKind.Expense, id, request);

    [HttpDelete("api/v1/income-subcategories/{id:long}")]
    public IActionResult DeleteIncome(long id) => Delete(EntryKind.Income, id);

    [HttpDelete("api/v1/expense-subcategories/{id:long}")]
    public IActionResult DeleteExpense(long id) => Delete(EntryKind.Expense, id);

    private IActionResult List(EntryKind kind, long? categoryId, bool? isActive)
    {
        var response = subcategories.List(kind, categoryId, isActive);
        return response is null ? Unauthorized() : Ok(response);
    }

    private IActionResult Get(EntryKind kind, long id)
    {
        if (!IsAuthenticated())
        {
            return Unauthorized();
        }

        var response = subcategories.Get(kind, id);
        return response is null ? NotFound() : Ok(response);
    }

    private IActionResult Create(EntryKind kind, string route, CreateSubcategoryRequest request)
    {
        var result = subcategories.Create(kind, request);
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

    private IActionResult Update(EntryKind kind, long id, UpdateSubcategoryRequest request)
    {
        var result = subcategories.Update(kind, id, request);
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
        subcategories.Deactivate(kind, id, out var unauthorized, out var notFound);
        if (unauthorized)
        {
            return Unauthorized();
        }

        return notFound ? NotFound() : NoContent();
    }

    private bool IsAuthenticated() => User.Identity?.IsAuthenticated == true;
}
