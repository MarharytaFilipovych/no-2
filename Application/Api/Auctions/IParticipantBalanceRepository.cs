namespace Application.Api.Auctions;

public interface IParticipantBalanceRepository
{
    Task<decimal> GetBalance(Guid userId);
    Task UpdateBalance(Guid userId, decimal amount);
    Task DepositFunds(Guid userId, decimal amount);
}