using Application.Api.Users;
using Application.Validators.Auth;

namespace Application.Commands.Auth;

using API.System;
using API.Users;
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
    IRefreshTokenGenerator refreshTokenGenerator,
    IEnumerable<ILoginValidator> validators)
    : IRequestHandler<LoginCommand, LoginCommand.Response>
{
    private const int MaxConcurrentSessions = 5;

    public async Task<LoginCommand.Response> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        var validationError = await ValidateLogin(request);
        if (validationError != null)
            return FailureResponse();

        var user = await usersRepository.GetUser(request.Email);
        if (user == null)
            return FailureResponse();

        await CleanupOldSessionIfNeeded(request.SessionId);
        await EnforceMaxConcurrentSessions(user.UserId);

        var session = await CreateSession(user.UserId);

        return SuccessResponse(user.UserId, session);
    }

    private async Task<LoginError?> ValidateLogin(LoginCommand command)
    {
        foreach (var validator in validators)
        {
            var error = await validator.Validate(command);
            if (error.HasValue)
                return error;
        }

        return null;
    }

    private async Task CleanupOldSessionIfNeeded(string? sessionId)
    {
        if (sessionId != null)
            await sessions.DropSession(sessionId);
    }

    private async Task EnforceMaxConcurrentSessions(Guid userId)
    {
        var activeSessions = await sessions.GetActiveSessions(userId);
        if (activeSessions.Count >= MaxConcurrentSessions)
        {
            var oldestSession = activeSessions
                .OrderBy(s => s.ExpirationTime)
                .First();
            await sessions.DropSession(oldestSession.SessionId);
        }
    }

    private async Task<UserSession> CreateSession(Guid userId)
    {
        var refreshToken = refreshTokenGenerator.GenerateRefreshToken();
        var session = new UserSession
        {
            RefreshToken = refreshToken.Value,
            SessionId = Guid.NewGuid().ToString(),
            ExpirationTime = refreshToken.ExpirationTime,
            UserId = userId
        };

        await sessions.SaveSession(session);
        return session;
    }

    private LoginCommand.Response SuccessResponse(Guid userId, UserSession session) =>
        new()
        {
            UserLoggedIn = true,
            RefreshToken = new RefreshToken
            {
                Value = session.RefreshToken,
                ExpirationTime = session.ExpirationTime
            },
            SessionId = session.SessionId,
            JwtToken = tokenGenerator.GenerateJwtToken(userId, new List<string>())
        };

    private static LoginCommand.Response FailureResponse() =>
        new() { UserLoggedIn = false };
}

public enum LoginError
{
    InvalidCredentials
}