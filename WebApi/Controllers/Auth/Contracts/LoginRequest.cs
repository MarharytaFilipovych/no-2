namespace WebApi.Controllers.Auth.Contracts;

using System.ComponentModel.DataAnnotations;

public class LoginRequest
{
    [Required]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; init; }
    
    [Required]
    public string Password { get; init; }
}