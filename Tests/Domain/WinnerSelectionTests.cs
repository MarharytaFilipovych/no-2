using Application.Api.Auctions;
using Domain.Auctions;
using Infrastructure.InMemory;

namespace Tests.Domain;

[TestFixture]
public class WinnerSelectionTests
{
    private IAuctionsRepository _auctionsRepository = null!;
    private IBidsRepository _bidsRepository = null!;

    [SetUp]
    public void SetUp()
    {
        _auctionsRepository = new AuctionsRepository();
        _bidsRepository = new BidsRepository();
    }

    [Test]
    public async Task SelectWinner_WithSingleBid_ShouldSelectThatBidder()
    {
        // Arrange
        var auction = await CreateEndedAuction(minPrice: 100);
        await PlaceBid(auction.AuctionId, userId: 1, amount: 150);

        // Act
        var highestBid = await _bidsRepository.GetHighestBidForAuction(auction.AuctionId);

        // Assert
        Assert.That(highestBid, Is.Not.Null);
        Assert.That(highestBid!.UserId, Is.EqualTo(1));
        Assert.That(highestBid.Amount, Is.EqualTo(150));
    }

    [Test]
    public async Task SelectWinner_WithMultipleBids_ShouldSelectHighest()
    {
        // Arrange
        var auction = await CreateEndedAuction(minPrice: 100);
        await PlaceBid(auction.AuctionId, userId: 1, amount: 150);
        await PlaceBid(auction.AuctionId, userId: 2, amount: 200);
        await PlaceBid(auction.AuctionId, userId: 3, amount: 175);

        // Act
        var highestBid = await _bidsRepository.GetHighestBidForAuction(auction.AuctionId);

        // Assert
        Assert.That(highestBid, Is.Not.Null);
        Assert.That(highestBid!.UserId, Is.EqualTo(2));
        Assert.That(highestBid.Amount, Is.EqualTo(200));
    }

    [Test]
    public async Task SelectWinner_WithTiedBids_Earliest_ShouldSelectFirstBidder()
    {
        // Arrange
        var auction = await CreateEndedAuction(
            minPrice: 100,
            tieBreaking: TieBreakingPolicy.Earliest
        );
        var time = DateTime.UtcNow;
        await PlaceBid(auction.AuctionId, userId: 1, amount: 200, placedAt: time);
        await PlaceBid(auction.AuctionId, userId: 2, amount: 200, placedAt: time.AddMinutes(1));
        await PlaceBid(auction.AuctionId, userId: 3, amount: 200, placedAt: time.AddMinutes(2));

        // Act
        var highestBid = await _bidsRepository.GetHighestBidForAuction(auction.AuctionId);

        // Assert
        Assert.That(highestBid, Is.Not.Null);
        Assert.That(highestBid!.UserId, Is.EqualTo(1));
    }

    [Test]
    public async Task SelectWinner_BelowMinPrice_ShouldReturnNoBids()
    {
        // Arrange
        var auction = await CreateEndedAuction(minPrice: 500);
        await PlaceBid(auction.AuctionId, userId: 1, amount: 150);
        await PlaceBid(auction.AuctionId, userId: 2, amount: 200);

        // Act
        var highestBid = await _bidsRepository.GetHighestBidForAuction(auction.AuctionId);

        // Assert
        Assert.That(highestBid, Is.Not.Null);
        Assert.That(highestBid!.Amount, Is.LessThan(auction.MinPrice));
    }

    [Test]
    public async Task SelectWinner_WithWithdrawnBids_ShouldIgnoreWithdrawn()
    {
        // Arrange
        var auction = await CreateEndedAuction(minPrice: 100);
        await PlaceBid(auction.AuctionId, userId: 1, amount: 150);
        var withdrawnBid = await PlaceBid(auction.AuctionId, userId: 2, amount: 200);
        await PlaceBid(auction.AuctionId, userId: 3, amount: 175);

        withdrawnBid.Withdraw();
        await _bidsRepository.UpdateBid(withdrawnBid);

        // Act
        var highestBid = await _bidsRepository.GetHighestBidForAuction(auction.AuctionId);

        // Assert
        Assert.That(highestBid, Is.Not.Null);
        Assert.That(highestBid!.UserId, Is.EqualTo(3));
        Assert.That(highestBid.Amount, Is.EqualTo(175));
    }

    [Test]
    public async Task SelectWinner_BlindAuction_ShouldConsiderAllNonWithdrawnBids()
    {
        // Arrange
        var auction = await CreateEndedBlindAuction(minPrice: 100);
        await PlaceBid(auction.AuctionId, userId: 1, amount: 50);
        await PlaceBid(auction.AuctionId, userId: 2, amount: 200);
        await PlaceBid(auction.AuctionId, userId: 3, amount: 150);

        // Act
        var highestBid = await _bidsRepository.GetHighestBidForAuction(auction.AuctionId);

        // Assert
        Assert.That(highestBid, Is.Not.Null);
        Assert.That(highestBid!.UserId, Is.EqualTo(2));
        Assert.That(highestBid.Amount, Is.EqualTo(200));
    }

    [Test]
    public async Task Finalize_WithWinner_ShouldSetWinnerAndAmount()
    {
        // Arrange
        var auction = await CreateEndedAuction(minPrice: 100);
        await PlaceBid(auction.AuctionId, userId: 1, amount: 150);
        var highestBid = await _bidsRepository.GetHighestBidForAuction(auction.AuctionId);

        // Act
        auction.Finalize(highestBid!.UserId, highestBid.Amount);
        await _auctionsRepository.UpdateAuction(auction);

        // Assert
        var finalized = await _auctionsRepository.GetAuction(auction.AuctionId);
        Assert.That(finalized!.State, Is.EqualTo(AuctionState.Finalized));
        Assert.That(finalized.WinnerId, Is.EqualTo(1));
        Assert.That(finalized.WinningBidAmount, Is.EqualTo(150));
    }

    [Test]
    public async Task Finalize_WithNoValidBids_ShouldSetNoWinner()
    {
        // Arrange
        var auction = await CreateEndedAuction(minPrice: 500);
        await PlaceBid(auction.AuctionId, userId: 1, amount: 150);

        // Act
        auction.Finalize(winnerId: null, winningAmount: null);
        await _auctionsRepository.UpdateAuction(auction);

        // Assert
        var finalized = await _auctionsRepository.GetAuction(auction.AuctionId);
        Assert.That(finalized!.State, Is.EqualTo(AuctionState.Finalized));
        Assert.That(finalized.WinnerId, Is.Null);
        Assert.That(finalized.WinningBidAmount, Is.Null);
    }

    [Test]
    public async Task Finalize_WhenNotEnded_ShouldThrowException()
    {
        // Arrange
        var auction = await CreateActiveAuction();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            auction.Finalize(winnerId: 1, winningAmount: 150));
    }

    [Test]
    public async Task Finalize_WhenAlreadyFinalized_ShouldThrowException()
    {
        // Arrange
        var auction = await CreateEndedAuction(minPrice: 100);
        auction.Finalize(winnerId: 1, winningAmount: 150);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            auction.Finalize(winnerId: 2, winningAmount: 200));
    }

    private async Task<Auction> CreateActiveAuction()
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
        auction.TransitionToActive();
        await _auctionsRepository.UpdateAuction(auction);
        return auction;
    }

    private async Task<Auction> CreateEndedAuction(
        decimal minPrice = 100,
        TieBreakingPolicy tieBreaking = TieBreakingPolicy.Earliest)
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test Auction",
            EndTime = DateTime.UtcNow.AddMinutes(-5),
            Type = AuctionType.Open,
            MinimumIncrement = 10,
            MinPrice = minPrice,
            TieBreakingPolicy = tieBreaking
        });
        auction.TransitionToActive();
        auction.TransitionToEnded();
        await _auctionsRepository.UpdateAuction(auction);
        return auction;
    }

    private async Task<Auction> CreateEndedBlindAuction(decimal minPrice = 100)
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test Auction",
            EndTime = DateTime.UtcNow.AddMinutes(-5),
            Type = AuctionType.Blind,
            MinPrice = minPrice,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });
        auction.TransitionToActive();
        auction.TransitionToEnded();
        await _auctionsRepository.UpdateAuction(auction);
        return auction;
    }

    private async Task<Bid> PlaceBid(
        int auctionId,
        int userId,
        decimal amount,
        DateTime? placedAt = null)
    {
        return await _bidsRepository.CreateBid(new Bid
        {
            AuctionId = auctionId,
            UserId = userId,
            Amount = amount,
            PlacedAt = placedAt ?? DateTime.UtcNow
        });
    }
}