namespace Application.Configs;

public interface IJwtTokenConfig
{
    string? Secret { get;}
    
    string Audience { get; }
        
    string Issuer { get; }
}


public class JwtTokenConfig : IJwtTokenConfig
{
    public string? Secret { get; set; }

    public string Audience { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;
}