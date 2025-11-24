namespace WebApi.Controllers.Auth;

using Application.API.System;
using Application.Commands.Auth;
using Contracts;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Hosting; 

[ApiController, Route("auth")]
public class AuthController(IMediator mediator, IWebHostEnvironment environment) : ControllerBase 
{
    private const string RefreshTokenKey = "refresh_token";
    private const string SessionIdKey = "session_id";

    [HttpPost("register")]
    [SwaggerResponse(StatusCodes.Status200OK, "Registration completed successfully", typeof(LoginResponse))]
    [SwaggerResponse(StatusCodes.Status409Conflict, "Email already taken")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var response = await mediator.Send(new RegisterCommand
            { Email = request.Email, Password = request.Password });
        if (response.Status.IsError)
        {
            return Conflict(); //409
        }

        UpdateRefreshTokenCookie(response.RefreshToken, response.SessionId);

        return Ok(new LoginResponse
        {
            AccessToken = response.JwtToken
        });
    }

    [HttpPost("login")]
    [SwaggerResponse(StatusCodes.Status200OK, "Login completed successfully", typeof(LoginResponse))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "Email or password is incorrect")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var response = await mediator.Send(new LoginCommand()
            { Email = request.Email, Password = request.Password });
        if (!response.UserLoggedIn)
        {
            return BadRequest(); 
        }
        UpdateRefreshTokenCookie(response.RefreshToken, response.SessionId);

        return Ok(new
        {
            AccessToken = response.JwtToken
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        if (!Request.Cookies.TryGetValue(RefreshTokenKey, out var refreshToken))
        {
            // This now executes when the browser refuses to send the cookie based on its security policy.
            return Forbid(); 
        }
        if (!Request.Cookies.TryGetValue(SessionIdKey, out var sessionId))
        {
            return Forbid();
        }

        var result = await mediator.Send(new RefreshTokenCommand
            { RefreshToken = refreshToken, SessionId = sessionId });

        if (result.Result.IsError)
        {
            return Forbid();
        }

        var newRefreshToken = result.RefreshToken;
        Response.Cookies.Append(RefreshTokenKey, newRefreshToken.Value, CookieOptions(newRefreshToken.ExpirationTime));

        return Ok(new LoginResponse() { AccessToken = result.JwtToken });
    }

    private CookieOptions CookieOptions(DateTime expirationTime) => new()
    {
        HttpOnly = true,
        // We need this part to allow token refreshing to work from Swagger calls both from Safari and Chromium
        Secure = !environment.IsDevelopment(),
        SameSite = environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None,
        Expires = expirationTime
    };

    private void UpdateRefreshTokenCookie(RefreshToken refreshToken, string sessionId)
    {
        var cookieOptions = CookieOptions(refreshToken.ExpirationTime);
        Response.Cookies.Append(RefreshTokenKey, refreshToken.Value, cookieOptions);
        Response.Cookies.Append(SessionIdKey, sessionId, cookieOptions);
    }
}