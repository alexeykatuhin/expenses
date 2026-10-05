using FamilyExpenses.Api.Models;
using FamilyExpenses.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FamilyExpenses.Api.Controllers;

[ApiController]
[Route("api/v1/reporting-periods")]
public sealed class ReportingPeriodsController(IReportingPeriodService periods) : ControllerBase
{
    [HttpGet]
    public IActionResult List()
    {
        var response = periods.List();
        return response is null ? Unauthorized() : Ok(response);
    }

    [HttpGet("active")]
    public IActionResult Active()
    {
        var response = periods.GetActive();
        return response is null ? Unauthorized() : Ok(response);
    }

    [HttpPost("{id:long}/close")]
    public IActionResult Close(long id)
    {
        var result = periods.Close(id);
        if (result.Unauthorized)
        {
            return Unauthorized();
        }

        if (result.NotFound)
        {
            return NotFound();
        }

        if (result.AlreadyClosed)
        {
            return Conflict();
        }

        if (result.EndsInFuture)
        {
            return BadRequest(ApiError.Validation([
                new ErrorDetail("period_id", "Reporting period cannot be closed before its end date")
            ]));
        }

        return Ok(result.Response);
    }
}
