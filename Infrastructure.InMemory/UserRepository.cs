namespace Infrastructure.InMemory;

using Application.Api;
using Domain.Users;

public class UserRepository : IUsersRepository
{
    private readonly List<UserWithPassword> users = new();

    public Task<bool> UserExists(string email)
    {
        return Task.FromResult(users.Any(user => user.Email == email));
    }

    public Task<bool> UserExists(int userId)
    {
        return Task.FromResult(users.Any(user => user.UserId == userId));
    }

    public Task<string?> GetUserPassword(string email)
    {
        return Task.FromResult(users.FirstOrDefault(u => u.Email == email)?.HashedPassword);
    }

    public Task<User?> GetUser(string email)
    {
        return Task.FromResult<User?>(users.FirstOrDefault(u => u.Email == email));
    }

    public Task<User?> CreateUser(string email, string hashedPassword)
    {
        var user = new UserWithPassword() { UserId = GetNextUserId(), HashedPassword = hashedPassword, Email = email};
        users.Add(user);
        return Task.FromResult<User?>(user);
    }

    private int GetNextUserId()
    {
        if (!users.Any())
        {
            return 1;
        }

        return users.Max(user => user.UserId) + 1;
    }
}

class UserWithPassword : User
{
    public string HashedPassword { get; init; }
}