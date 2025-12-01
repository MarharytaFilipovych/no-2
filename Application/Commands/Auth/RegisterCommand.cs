using Application.Api.Users;
using Application.Validators.Auth;

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
    IRefreshTokenGenerator refreshTokenGenerator,
    IEnumerable<IRegisterValidator> validators)
    : IRequestHandler<RegisterCommand, RegisterCommand.Response>
{
    public async Task<RegisterCommand.Response> Handle(
        RegisterCommand request,
        CancellationToken cancellationToken)
    {
        var validationError = await ValidateRegistration(request);
        if (validationError != null) return ErrorResponse(validationError.Value);

        var user = await CreateUser(request);
        var session = await CreateSession(user.UserId);

        return SuccessResponse(user.UserId, session);
    }

    private async Task<RegistrationError?> ValidateRegistration(RegisterCommand command)
    {
        foreach (var validator in validators)
        {
            var error = await validator.Validate(command);
            if (error.HasValue) return error;
        }

        return null;
    }

    private async Task<Domain.Users.User> CreateUser(RegisterCommand request)
    {
        var hashed = BCrypt.HashPassword(request.Password);
        return await users!.CreateUser(request.Email, hashed);
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

    private RegisterCommand.Response SuccessResponse(Guid userId, UserSession session) =>
        new()
        {
            Status = OkOrError<RegistrationError>.Ok(),
            SessionId = session.SessionId,
            RefreshToken = new RefreshToken
            {
                Value = session.RefreshToken,
                ExpirationTime = session.ExpirationTime
            },
            JwtToken = tokenGenerator.GenerateJwtToken(userId, new List<string>())
        };

    private static RegisterCommand.Response ErrorResponse(RegistrationError error) =>
        new() { Status = OkOrError<RegistrationError>.Error(error) };
}