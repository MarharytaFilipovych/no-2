namespace Application.Shared.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class ServicesExtensions
{
    public static TConfig InstallConfigFromSection<TInterface, TConfig>(this WebApplicationBuilder builder,
        string section)
        where TConfig : TInterface 
        where TInterface : class
    {
        var configurationSection = builder.Configuration.GetSection(section);
        var config = configurationSection.Get<TConfig>()!;
        builder.Services.AddSingleton<TInterface>(config);

        return config;
    }
}