namespace Application.Auth;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using API.System;
using Microsoft.IdentityModel.Tokens;
using Configs;

public class JwtTokenGenerator(IJwtTokenConfig config) : IJwtTokenGenerator
{
    public string GenerateJwtToken(Guid id, IEnumerable<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(permissions.Select(permission => new Claim("claims", permission)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: config.Issuer,
            audience: config.Audience,
            claims: claims,
            expires: DateTime.Now.AddMinutes(15),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}