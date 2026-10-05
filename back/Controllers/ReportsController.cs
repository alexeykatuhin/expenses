using FamilyExpenses.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FamilyExpenses.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
public sealed class ReportsController(IReportService reports) : ControllerBase
{
    [HttpGet("summary")]
    public IActionResult Summary([FromQuery(Name = "date_from")] DateOnly? dateFrom, [FromQuery(Name = "date_to")] DateOnly? dateTo, [FromQuery(Name = "period_id")] long? periodId)
    {
        var response = reports.Summary(dateFrom, dateTo, periodId);
        return response is null ? Unauthorized() : Ok(response);
    }

    [HttpGet("by-category")]
    public IActionResult ByCategory([FromQuery] string type, [FromQuery(Name = "date_from")] DateOnly? dateFrom, [FromQuery(Name = "date_to")] DateOnly? dateTo, [FromQuery(Name = "period_id")] long? periodId) =>
        Grouped(type, dateFrom, dateTo, bySubcategory: false, periodId);

    [HttpGet("by-subcategory")]
    public IActionResult BySubcategory([FromQuery] string type, [FromQuery(Name = "date_from")] DateOnly? dateFrom, [FromQuery(Name = "date_to")] DateOnly? dateTo, [FromQuery(Name = "period_id")] long? periodId) =>
        Grouped(type, dateFrom, dateTo, bySubcategory: true, periodId);

    [HttpGet("by-period-category")]
    public IActionResult ByPeriodCategory([FromQuery] string type, [FromQuery(Name = "date_from")] DateOnly? dateFrom, [FromQuery(Name = "date_to")] DateOnly? dateTo)
    {
        var result = reports.PeriodCategory(type, dateFrom, dateTo);
        if (result.Unauthorized)
        {
            return Unauthorized();
        }

        return result.Error is not null ? BadRequest(result.Error) : Ok(result.Response);
    }

    private IActionResult Grouped(string type, DateOnly? dateFrom, DateOnly? dateTo, bool bySubcategory, long? periodId = null)
    {
        var result = reports.Grouped(type, dateFrom, dateTo, bySubcategory, periodId);
        if (result.Unauthorized)
        {
            return Unauthorized();
        }

        return result.Error is not null ? BadRequest(result.Error) : Ok(result.Response);
    }
}
