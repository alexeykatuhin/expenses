using FamilyExpenses.Api.Models;
using FamilyExpenses.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FamilyExpenses.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
public sealed class UsersController(IUserService users) : ControllerBase
{
    [HttpGet("me")]
    public IActionResult Me()
    {
        var response = users.GetCurrent();
        return response is null ? Unauthorized() : Ok(response);
    }

    [HttpPatch("me")]
    public IActionResult UpdateMe(UpdateUserRequest request)
    {
        var result = users.UpdateCurrent(request);
        if (result.Unauthorized)
        {
            return Unauthorized();
        }

        return result.Error is not null ? BadRequest(result.Error) : Ok(result.Response);
    }
}
