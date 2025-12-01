using Application.Api.Utils;

namespace Application.Auth;

using API.System;
using System.Security.Cryptography;
using Utils;

public class RefreshTokenGenerator(ITimeProvider timeProvider) : IRefreshTokenGenerator
{
    public RefreshToken GenerateRefreshToken()
    {
        var randomNumber = new byte[32]; // 256 bits
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomNumber);
        }
        
        var value = Convert.ToBase64String(randomNumber);
        return new RefreshToken()
        {
            Value = value,
            ExpirationTime = timeProvider.Now() + TimeSpan.FromDays(7)
        };
    }
}