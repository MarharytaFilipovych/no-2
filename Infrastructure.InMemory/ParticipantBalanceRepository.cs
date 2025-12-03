using System.Collections.Concurrent;
using Application.Api.Auctions;

namespace Infrastructure.InMemory;

public class ParticipantBalanceRepository : IParticipantBalanceRepository
{
    private readonly ConcurrentDictionary<Guid, decimal> _balances = new();

    public Task<decimal> GetBalance(Guid userId)
    {
        _balances.TryGetValue(userId, out var balance);
        return Task.FromResult(balance);
    }

    public Task UpdateBalance(Guid userId, decimal amount)
    {
        _balances[userId] = amount;
        return Task.CompletedTask;
    }

    public Task DepositFunds(Guid userId, decimal amount)
    {
        _balances.AddOrUpdate(userId, amount, (_, current) => current + amount);
        return Task.CompletedTask;
    }
}