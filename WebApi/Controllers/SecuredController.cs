namespace WebApi.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController, Route("secured"), Authorize]
public class SecuredController : ControllerBase
{
    [HttpGet]
    public IActionResult GetSecuredData()
    {
        return Ok($"Your're inside secured area, user#{User.FindFirst(ClaimTypes.NameIdentifier)?.Value}!");
    }
}