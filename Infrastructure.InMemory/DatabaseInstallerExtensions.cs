using Application.Api.Auctions;
using Application.Api.Users;

namespace Infrastructure.InMemory;

using Application.Api;
using Application.API.Users;
using Microsoft.Extensions.DependencyInjection;

public static class InMemoryInstallerExtensions
{
    public static void AddPersistence(this IServiceCollection services)
    {
        services.AddSingleton<IUsersRepository, UserRepository>();
        services.AddSingleton<ISessionsRepository, SessionsRepository>();
        services.AddSingleton<IAuctionsRepository, AuctionsRepository>();
        services.AddSingleton<IBidsRepository, BidsRepository>();
    }
}