using FamilyExpenses.Api.Models;
using FamilyExpenses.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FamilyExpenses.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("register")]
    public IActionResult Register(RegisterRequest request)
    {
        var result = auth.Register(request, HttpContext);
        if (result.Error is not null)
        {
            return BadRequest(result.Error);
        }

        return Created("/api/v1/users/me", result.Response);
    }

    [HttpPost("login")]
    public IActionResult Login(LoginRequest request)
    {
        var response = auth.Login(request, HttpContext);
        return response is null ? Unauthorized() : Ok(response);
    }

    [HttpPost("refresh")]
    public IActionResult Refresh(RefreshRequest request)
    {
        var response = auth.Refresh(request, HttpContext);
        return response is null ? Unauthorized() : Ok(response);
    }

    [HttpPost("logout")]
    public IActionResult Logout(RefreshRequest request) =>
        auth.Logout(request) ? NoContent() : Unauthorized();
}
