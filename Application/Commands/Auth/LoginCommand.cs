using Application.Api.Users;

namespace Application.Commands.Auth;

using API.System;
using API.Users;
using BCrypt.Net;
using MediatR;

public class LoginCommand : IRequest<LoginCommand.Response>
{
    public required string Email { get; init; }

    public required string Password { get; init; }

    public string? SessionId { get; init; }

    public class Response
    {
        public bool UserLoggedIn { get; init; }

        public string? JwtToken { get; init; }

        public string? SessionId { get; init; }

        public RefreshToken? RefreshToken { get; init; }
    }
}

public class LoginCommandHandler(
    IUsersRepository usersRepository,
    IJwtTokenGenerator tokenGenerator,
    ISessionsRepository sessions,
    IRefreshTokenGenerator refreshTokenGenerator
)
    : IRequestHandler<LoginCommand, LoginCommand.Response>
{
    private const int MaxConcurrentSessions = 5;

    public async Task<LoginCommand.Response> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var pass = await usersRepository.GetUserPassword(request.Email);
        if (pass == null || !BCrypt.Verify(request.Password, pass)) 
            return new LoginCommand.Response { UserLoggedIn = false };

        var user = await usersRepository.GetUser(request.Email);
        if (user == null)
        {
            return new LoginCommand.Response
            {
                UserLoggedIn = false
            };
        }

        if (request.SessionId != null) 
            await sessions.DropSession(request.SessionId);

        var activeSessions = await sessions.GetActiveSessions(user.UserId);
        if (activeSessions.Count == MaxConcurrentSessions)
        {
            var oldestSession = activeSessions.OrderBy(userSession => userSession.ExpirationTime).First();
            await sessions.DropSession(oldestSession.SessionId);
        }

        var refreshToken = refreshTokenGenerator.GenerateRefreshToken();
        var session = new UserSession
        {
            RefreshToken = refreshToken.Value,
            SessionId = Guid.NewGuid().ToString(),
            ExpirationTime = refreshToken.ExpirationTime,
            UserId = user.UserId
        };

        await sessions.SaveSession(session);

        return new LoginCommand.Response
        {
            UserLoggedIn = true,
            RefreshToken = refreshToken,
            SessionId = session.SessionId,
            JwtToken = tokenGenerator.GenerateJwtToken(user.UserId, new List<string>())
        };
    }
}