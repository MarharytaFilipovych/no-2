namespace Application.API.System;

public interface IJwtTokenGenerator
{
    string GenerateJwtToken(int id, IEnumerable<string> permissions);
}