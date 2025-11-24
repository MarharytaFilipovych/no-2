namespace Application.API.System;

public interface IRefreshTokenGenerator
{
    RefreshToken GenerateRefreshToken();
}

public class RefreshToken
{
    public string Value { get; init; }
    
    public DateTime ExpirationTime { get; init; }
}