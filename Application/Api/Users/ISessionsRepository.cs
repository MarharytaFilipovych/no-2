namespace Application.API.Users;

public interface ISessionsRepository
{
    Task<List<UserSession>> GetActiveSessions(int user);

    Task DropSession(string sessionId);
    
    Task SaveSession(UserSession session);

    Task<UserSession?> GetUserSession(string session);

    Task UpdateRefreshToken(string session, string token, DateTime expirationTime);
}

public class UserSession
{
    public DateTime ExpirationTime { get; init; }
    
    public string SessionId { get; init; }
    
    public string RefreshToken { get; init; }
    
    public int UserId { get; init; }
}