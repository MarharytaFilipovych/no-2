using Application.Commands.Auth;

namespace Application.Validators.Auth;

public interface IRefreshTokenValidator
{
    Task<RefreshTokenError?> Validate(string sessionId, string refreshToken);
}

public class SessionExistsValidator(API.Users.ISessionsRepository sessionsRepository) : IRefreshTokenValidator
{
    public async Task<RefreshTokenError?> Validate(string sessionId, string refreshToken)
    {
        var session = await sessionsRepository.GetUserSession(sessionId);
        return session == null ? RefreshTokenError.UnknownSession : null;
    }
}

public class RefreshTokenMatchValidator(API.Users.ISessionsRepository sessionsRepository) : IRefreshTokenValidator
{
    public async Task<RefreshTokenError?> Validate(string sessionId, string refreshToken)
    {
        var session = await sessionsRepository.GetUserSession(sessionId);
        if (session == null) return null;

        return session.RefreshToken != refreshToken
            ? RefreshTokenError.WrongToken : null;
    }
}