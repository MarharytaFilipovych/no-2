using Application.Validators.Auth;

namespace Application.Commands.Auth;

using API.System;
using API.Users;
using MediatR;
using Utils;

public class RefreshTokenCommand : IRequest<RefreshTokenCommand.Response>
{
    public required string SessionId { get; init; }
    public required string RefreshToken { get; init; }

    public class Response
    {
        public static Response Error(RefreshTokenError error) =>
            new() { Result = OkOrError<RefreshTokenError>.Error(error) };

        public OkOrError<RefreshTokenError> Result { get; init; }
        public string? JwtToken { get; init; }
        public RefreshToken? RefreshToken { get; init; }
    }
}

public enum RefreshTokenError
{
    UnknownSession,
    WrongToken
}

public class RefreshTokenCommandHandler(
    ISessionsRepository sessions,
    IJwtTokenGenerator jwtGenerator,
    IRefreshTokenGenerator refreshTokenGenerator,
    IEnumerable<IRefreshTokenValidator> validators)
    : IRequestHandler<RefreshTokenCommand, RefreshTokenCommand.Response>
{
    public async Task<RefreshTokenCommand.Response> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        var validationError = await ValidateRefreshToken(request.SessionId, request.RefreshToken);
        if (validationError != null) return ErrorResponse(validationError.Value);

        var session = await sessions.GetUserSession(request.SessionId);
        if (session == null) return ErrorResponse(RefreshTokenError.UnknownSession);

        var newRefreshToken = refreshTokenGenerator.GenerateRefreshToken();
        await UpdateSession(session.SessionId, newRefreshToken);

        return SuccessResponse(session.UserId, newRefreshToken);
    }

    private async Task<RefreshTokenError?> ValidateRefreshToken(string sessionId, string refreshToken)
    {
        foreach (var validator in validators)
        {
            var error = await validator.Validate(sessionId, refreshToken);
            if (error.HasValue) return error;
        }

        return null;
    }

    private async Task UpdateSession(string sessionId, RefreshToken newRefreshToken)
    {
        await sessions.UpdateRefreshToken(sessionId, newRefreshToken.Value,
            newRefreshToken.ExpirationTime);
    }

    private RefreshTokenCommand.Response SuccessResponse(Guid userId, RefreshToken refreshToken) =>
        new()
        {
            Result = OkOrError<RefreshTokenError>.Ok(),
            RefreshToken = refreshToken,
            JwtToken = jwtGenerator.GenerateJwtToken(userId, new List<string>())
        };

    private static RefreshTokenCommand.Response ErrorResponse(RefreshTokenError error) =>
        RefreshTokenCommand.Response.Error(error);
}