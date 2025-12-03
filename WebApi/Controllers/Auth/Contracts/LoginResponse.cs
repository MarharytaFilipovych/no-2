namespace WebApi.Controllers.Auth.Contracts;

public class LoginResponse
{
    public required string AccessToken { get; init; }
}