using System.Collections.Concurrent;
using Application.Api.Users;

namespace Infrastructure.InMemory;

using Application.Api;
using Domain.Users;

public class UserRepository : IUsersRepository
{
    private readonly ConcurrentDictionary<string, UserWithPassword> _usersByEmail = new();
    private readonly ConcurrentDictionary<Guid, UserWithPassword> _usersById = new();

    public Task<bool> UserExists(string email) =>
        Task.FromResult(_usersByEmail.ContainsKey(email));

    public Task<bool> UserExists(Guid userId) =>
        Task.FromResult(_usersById.ContainsKey(userId));

    public Task<string?> GetUserPassword(string email) =>
        Task.FromResult(_usersByEmail.TryGetValue(email, out var user) ? user.HashedPassword : null);

    public Task<User?> GetUser(string email) =>
        Task.FromResult<User?>(_usersByEmail.TryGetValue(email, out var user) ? user : null);

    public Task<User?> CreateUser(string email, string hashedPassword)
    {
        var user = new UserWithPassword
        {
            UserId = Guid.NewGuid(),
            HashedPassword = hashedPassword,
            Email = email
        };

        _usersByEmail.TryAdd(email, user);
        _usersById.TryAdd(user.UserId, user);

        return Task.FromResult<User?>(user);
    }

    public Task UpdateUser(User user)
    {
        if (_usersById.ContainsKey(user.UserId))
        {
            var userWithPassword = _usersById[user.UserId];
            userWithPassword.BannedUntil = user.BannedUntil;
            _usersById[user.UserId] = userWithPassword;

            if (userWithPassword.Email != null)
            {
                _usersByEmail[userWithPassword.Email] = userWithPassword;
            }
        }

        return Task.CompletedTask;
    }

    public Task<User?> GetUser(Guid userId)
    {
        _usersById.TryGetValue(userId, out var user);
        return Task.FromResult<User?>(user);
    }
}

class UserWithPassword : User
{
    public required string HashedPassword { get; init; }
}