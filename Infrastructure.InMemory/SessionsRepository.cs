namespace Infrastructure.InMemory;

using Application.API.Users;

public class SessionsRepository : ISessionsRepository
{
    private readonly List<UserSession> _allSessions = [];

    public Task<List<UserSession>> GetActiveSessions(Guid user)
    {
        return Task.FromResult(_allSessions.Where(session => session.UserId == user).ToList());
    }

    public async Task DropSession(string sessionId)
    {
        var session = await GetUserSession(sessionId);
        if (session != null)
        {
            _allSessions.Remove(session);
        }
    }

    public Task SaveSession(UserSession session)
    {
        _allSessions.Add(session);
        return Task.CompletedTask;
    }

    public Task<UserSession?> GetUserSession(string session)
    {
        return Task.FromResult(_allSessions.FirstOrDefault(userSession => userSession.SessionId == session));
    }

    public async Task UpdateRefreshToken(string sessionId, string token, DateTime expirationTime)
    {
        var session = await GetUserSession(sessionId);
        if (session == null)
        {
            return;
        }
        
        await DropSession(sessionId);
        await SaveSession(new UserSession
        {
            RefreshToken = token,
            SessionId = sessionId,
            UserId = session.UserId,
            ExpirationTime = expirationTime
        });
    }
}