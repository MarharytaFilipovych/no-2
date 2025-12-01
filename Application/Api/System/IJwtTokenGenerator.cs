namespace Application.API.System;

public interface IJwtTokenGenerator
{
    string GenerateJwtToken(Guid id, IEnumerable<string> permissions);
}