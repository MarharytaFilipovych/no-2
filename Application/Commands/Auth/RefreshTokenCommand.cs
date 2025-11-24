namespace Application.Commands.Auth;

using Api;
using API.System;
using API.Users;
using Domain.Users;
using MediatR;
using Utils;

public class RefreshTokenCommand : IRequest<RefreshTokenCommand.Response>
{
    public string SessionId { get; init; }

    public string RefreshToken { get; init; }

    public class Response
    {
        public static Response Error(RefreshTokenError error) =>
            new() { Result = OkOrError<RefreshTokenError>.Error(error) };

        public OkOrError<RefreshTokenError> Result { get; init; }

        public string JwtToken { get; init; }

        public RefreshToken RefreshToken { get; init; }
    }
}

public class RefreshTokenCommandHandler(
    ISessionsRepository sessions,
    IJwtTokenGenerator jwtGenerator,
    IUsersRepository usersRepository,
    IRefreshTokenGenerator refreshTokenGenerator)
    : IRequestHandler<RefreshTokenCommand, RefreshTokenCommand.Response>
{

    public async Task<RefreshTokenCommand.Response> Handle(RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        var session = await sessions.GetUserSession(request.SessionId);
        if (session == null)
        {
            return RefreshTokenCommand.Response.Error(RefreshTokenError.UnknownSession);
        }

        if (session.RefreshToken != request.RefreshToken)
        {
            return RefreshTokenCommand.Response.Error(RefreshTokenError.WrongToken);
        }

        var newToken =
            jwtGenerator.GenerateJwtToken(session.UserId, new List<string>());
        var newRefreshToken = refreshTokenGenerator.GenerateRefreshToken();

        await sessions.UpdateRefreshToken(session.SessionId, newRefreshToken.Value, newRefreshToken.ExpirationTime);
        return new RefreshTokenCommand.Response
        {
            Result = OkOrError<RefreshTokenError>.Ok(),
            RefreshToken = newRefreshToken,
            JwtToken = newToken
        };
    }
}

public enum RefreshTokenError
{
    UnknownSession,
    WrongToken
}