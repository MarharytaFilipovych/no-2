using Application.Api.Auctions;
using Domain.Auctions;
using Infrastructure.InMemory;

namespace Tests.Repositories;

[TestFixture]
public class AuctionRepositoryTests
{
    private IAuctionsRepository _auctionsRepository = null!;

    [SetUp]
    public void SetUp()
    {
        _auctionsRepository = new AuctionsRepository();
    }

    [Test]
    public async Task GetAuctionsByState_ShouldReturnOnlyMatchingState()
    {
        // Arrange
        await CreateAuction(AuctionState.Pending);
        await CreateAuction(AuctionState.Active);
        await CreateAuction(AuctionState.Ended);

        // Act
        var activeAuctions = await _auctionsRepository.GetAuctionsByState(AuctionState.Active);

        // Assert
        Assert.That(activeAuctions.Count, Is.EqualTo(1));
        Assert.That(activeAuctions[0].State, Is.EqualTo(AuctionState.Active));
    }

    [Test]
    public async Task GetAuction_WhenExists_ShouldReturnAuction()
    {
        // Arrange
        var auction = await CreateAuction(AuctionState.Pending);

        // Act
        var retrieved = await _auctionsRepository.GetAuction(auction.AuctionId);

        // Assert
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.AuctionId, Is.EqualTo(auction.AuctionId));
    }

    [Test]
    public async Task GetAuction_WhenNotExists_ShouldReturnNull()
    {
        // Act
        var retrieved = await _auctionsRepository.GetAuction(999);
        
        // Assert
        Assert.That(retrieved, Is.Null);
    }

    [Test]
    public async Task UpdateAuction_ShouldPersistChanges()
    {
        // Arrange
        var auction = await CreateAuction(AuctionState.Pending);
        
        // Act
        auction.TransitionToActive();
        await _auctionsRepository.UpdateAuction(auction);

        // Assert
        var retrieved = await _auctionsRepository.GetAuction(auction.AuctionId);
        Assert.That(retrieved!.State, Is.EqualTo(AuctionState.Active));
    }

    private async Task<Auction> CreateAuction(AuctionState initialState)
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test Auction",
            EndTime = DateTime.UtcNow.AddHours(1),
            Type = AuctionType.Open,
            MinimumIncrement = 10,
            MinPrice = 100,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });

        switch (initialState)
        {
            case AuctionState.Active:
                auction.TransitionToActive();
                await _auctionsRepository.UpdateAuction(auction);
                break;
            case AuctionState.Ended:
                auction.TransitionToActive();
                auction.TransitionToEnded();
                await _auctionsRepository.UpdateAuction(auction);
                break;
            case AuctionState.Finalized:
                auction.TransitionToActive();
                auction.TransitionToEnded();
                auction.Finalize(1, 150m);
                await _auctionsRepository.UpdateAuction(auction);
                break;
            case AuctionState.Pending:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(initialState), initialState, null);
        }

        return auction;
    }
}