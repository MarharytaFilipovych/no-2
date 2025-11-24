namespace Application.Api;

using Domain.Users;

public interface IUsersRepository
{
    Task<bool> UserExists(string email);
    
    Task<bool> UserExists(int userId);

    Task<string?> GetUserPassword(string email);

    Task<User?> GetUser(string email);

    Task<User?> CreateUser(string email, string hashedPassword);
}