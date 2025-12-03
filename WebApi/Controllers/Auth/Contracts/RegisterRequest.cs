namespace WebApi.Controllers.Auth.Contracts;

using System.ComponentModel.DataAnnotations;

public class RegisterRequest
{
    [Required]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public required string Email { get; init; }
    
    [Required]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters long.")]
    public required string Password { get; init; }
}