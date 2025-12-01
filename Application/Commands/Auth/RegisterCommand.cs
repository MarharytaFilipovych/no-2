using Application.Api.Users;

namespace Application.Commands.Auth;

using API.System;
using API.Users;
using BCrypt.Net;
using MediatR;
using Utils;

public class RegisterCommand : IRequest<RegisterCommand.Response>
{
    public required string Email { get; init; }

    public required string Password { get; init; }

    public class Response
    {
        public OkOrError<RegistrationError> Status { get; init; }

        public string? JwtToken { get; init; }

        public string? SessionId { get; init; }

        public RefreshToken? RefreshToken { get; init; }
    }
}

public enum RegistrationError
{
    AlreadyExists
}

public class RegisterCommandHandler(
    IUsersRepository users,
    IJwtTokenGenerator tokenGenerator,
    ISessionsRepository sessions,
    IRefreshTokenGenerator refreshTokenGenerator) : IRequestHandler<RegisterCommand, RegisterCommand.Response>
{

    public async Task<RegisterCommand.Response> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (await users.UserExists(request.Email))
        {
            return new RegisterCommand.Response
                { Status = OkOrError<RegistrationError>.Error(RegistrationError.AlreadyExists) };
        }

        var hashed = BCrypt.HashPassword(request.Password);

        var user = await users.CreateUser(request.Email, hashed);
        var refreshToken = refreshTokenGenerator.GenerateRefreshToken();
        var session = new UserSession
        {
            RefreshToken = refreshToken.Value,
            SessionId = Guid.NewGuid().ToString(),
            ExpirationTime = refreshToken.ExpirationTime,
            UserId = user!.UserId
        };
        
        await sessions.SaveSession(session);
        return new RegisterCommand.Response
        {
            Status = OkOrError<RegistrationError>.Ok(),
            SessionId = session.SessionId,
            RefreshToken = refreshToken,
            JwtToken = tokenGenerator.GenerateJwtToken(user.UserId, new List<string>())
        };
    }
}