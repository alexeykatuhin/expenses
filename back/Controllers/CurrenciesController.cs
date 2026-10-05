using FamilyExpenses.Api.Models;
using FamilyExpenses.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FamilyExpenses.Api.Controllers;

[ApiController]
[Route("api/v1/currencies")]
public sealed class CurrenciesController(ICurrencyService currencies) : ControllerBase
{
    [HttpGet]
    public IActionResult List()
    {
        var response = currencies.List();
        return response is null ? Unauthorized() : Ok(response);
    }

    [HttpPost]
    public IActionResult Create(CreateCurrencyRequest request)
    {
        var result = currencies.Create(request);
        if (result.Unauthorized)
        {
            return Unauthorized();
        }

        if (result.Error is not null)
        {
            return BadRequest(result.Error);
        }

        return Created($"/api/v1/currencies/{result.Response!.Code}", result.Response);
    }
}
