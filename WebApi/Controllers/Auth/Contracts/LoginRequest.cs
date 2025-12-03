namespace WebApi.Controllers.Auth.Contracts;

using System.ComponentModel.DataAnnotations;

public class LoginRequest
{
    [Required]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public required string Email { get; init; }
    
    [Required]
    public required string Password { get; init; }
}