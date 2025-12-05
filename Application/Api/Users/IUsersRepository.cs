using Domain.Users;

namespace Application.Api.Users;

public interface IUsersRepository
{
    Task<bool> UserExists(string email);

    Task<bool> UserExists(Guid userId);

    Task<string?> GetUserPassword(string email);

    Task<User?> GetUser(string email);

    Task<User?> CreateUser(string email, string hashedPassword);
    Task UpdateUser(User user);
    Task<User?> GetUser(Guid userId);
}