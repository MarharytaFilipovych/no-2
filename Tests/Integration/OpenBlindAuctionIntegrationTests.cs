using Application.Api.Auctions;
using Application.Commands.Auctions;
using Application.Queries.Auctions;
using Application.Validators.Auctions;
using Domain.Auctions;
using Infrastructure.InMemory;
using Tests.Utils;

namespace Tests.Integration;

[TestFixture]
public class OpenBlindAuctionIntegrationTests
{
    private IAuctionsRepository _auctionsRepository = null!;
    private IBidsRepository _bidsRepository = null!;
    private IParticipantBalanceRepository _balanceRepository = null!;
    private TestTimeProvider _timeProvider = null!;
    private TestBiddingConfig _biddingConfig = null!;
    private AuctionVisibilityService _visibilityService = null!;

    [SetUp]
    public void SetUp()
    {
        _auctionsRepository = new AuctionsRepository();
        _bidsRepository = new BidsRepository();
        _balanceRepository = new ParticipantBalanceRepository();
        _timeProvider = new TestTimeProvider();
        _biddingConfig = new TestBiddingConfig
        {
            MaxBidAmount = 1000000,
            BalanceRatioLimit = 0.5m
        };
        _visibilityService = new AuctionVisibilityService();
    }


    [Test]
    public async Task OpenAuction_MultipleBidsFromSameUser_ShouldAllBeVisible()
    {
        // Arrange
        var auction = await CreateActiveOpenAuction();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        await SetupUserBalance(user1, 10000);
        await SetupUserBalance(user2, 10000);

        // Act - User places multiple bids
        await PlaceBid(auction.Id, user1, 110);
        await PlaceBid(auction.Id, user2, 130);
        await PlaceBid(auction.Id, user1, 150);

        // Assert
        var allBids = await _bidsRepository.GetBidsByAuction(auction.Id);
        Assert.That(allBids.Count, Is.EqualTo(3));
        Assert.That(allBids.Count(b => b.UserId == user1), Is.EqualTo(2));
    }

    [Test]
    public async Task OpenAuction_Active_AllBidsShouldBeVisibleToAnonymous()
    {
        // Arrange
        var auction = await CreateActiveOpenAuction();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        await SetupUserBalance(user1, 10000);
        await SetupUserBalance(user2, 10000);
        await PlaceBid(auction.Id, user1, 110);
        await PlaceBid(auction.Id, user2, 130);

        // Act
        var query = new GetAuctionBidsQuery
        {
            AuctionId = auction.Id,
            RequestingUserId = null
        };
        var handler = new GetAuctionBidsQueryHandler(
            _auctionsRepository, _bidsRepository, _visibilityService);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.That(result.Bids.Count, Is.EqualTo(2));
        Assert.That(result.Bids.All(b => b.Amount.HasValue), Is.True);
        Assert.That(result.Bids.All(b => !b.AmountHidden), Is.True);
    }

    [Test]
    public async Task OpenAuction_HighestBidShouldBeVisible()
    {
        // Arrange
        var auction = await CreateActiveOpenAuction();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        await SetupUserBalance(user1, 10000);
        await SetupUserBalance(user2, 10000);
        await PlaceBid(auction.Id, user1, 110);
        await PlaceBid(auction.Id, user2, 130);

        // Act
        var query = new GetAuctionDetailsQuery
        {
            AuctionId = auction.Id,
            RequestingUserId = null
        };
        var handler = new GetAuctionDetailsQueryHandler(
            _auctionsRepository, _bidsRepository, _visibilityService);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.That(result.Auction, Is.Not.Null);
        Assert.That(result.Auction!.CurrentHighestBid, Is.EqualTo(130));
    }

    [Test]
    public async Task OpenAuction_MinPriceShouldAlwaysBeVisible()
    {
        // Arrange
        var auction = await CreateActiveOpenAuction(minPrice: 500);

        // Act
        var query = new GetAuctionDetailsQuery
        {
            AuctionId = auction.Id,
            RequestingUserId = null
        };
        var handler = new GetAuctionDetailsQueryHandler(
            _auctionsRepository, _bidsRepository, _visibilityService);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.That(result.Auction!.MinPrice, Is.EqualTo(500));
    }

    [Test]
    public async Task BlindAuction_SecondBidFromSameUser_ShouldReplaceFirst()
    {
        // Arrange
        var auction = await CreateActiveBlindAuction();
        var userId = Guid.NewGuid();
        await SetupUserBalance(userId, 10000);

        // Act
        var firstBid = await PlaceBid(auction.Id, userId, 110);
        
        // Simulate placing second bid which should withdraw the first
        var handler = CreatePlaceBidHandler();
        await handler.Handle(new PlaceBidCommand
        {
            AuctionId = auction.Id,
            UserId = userId,
            Amount = 150
        }, CancellationToken.None);

        // Assert
        var updatedFirstBid = await _bidsRepository.GetBid(firstBid.Id);
        var allBids = await _bidsRepository.GetActiveBidsByAuction(auction.Id);
        
        Assert.That(updatedFirstBid!.IsWithdrawn, Is.True);
        Assert.That(allBids.Count(b => b.UserId == userId), Is.EqualTo(1));
        Assert.That(allBids.First(b => b.UserId == userId).Amount, Is.EqualTo(150));
    }

    [Test]
    public async Task BlindAuction_Active_BidAmountsShouldBeHiddenFromOthers()
    {
        // Arrange
        var auction = await CreateActiveBlindAuction();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        await SetupUserBalance(user1, 10000);
        await SetupUserBalance(user2, 10000);
        await PlaceBid(auction.Id, user1, 110);
        await PlaceBid(auction.Id, user2, 150);

        // Act - User2 trying to see User1's bid
        var query = new GetAuctionBidsQuery
        {
            AuctionId = auction.Id,
            RequestingUserId = user2
        };
        var handler = new GetAuctionBidsQueryHandler(
            _auctionsRepository, _bidsRepository, _visibilityService);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        var user1Bid = result.Bids.FirstOrDefault(b => b.UserId == user1);
        Assert.That(user1Bid, Is.Not.Null);
        Assert.That(user1Bid!.Amount, Is.Null);
        Assert.That(user1Bid.AmountHidden, Is.True);
    }

    [Test]
    public async Task BlindAuction_Active_UserShouldSeeOwnBid()
    {
        // Arrange
        var auction = await CreateActiveBlindAuction();
        var userId = Guid.NewGuid();
        await SetupUserBalance(userId, 10000);
        await PlaceBid(auction.Id, userId, 110);

        // Act
        var query = new GetAuctionBidsQuery
        {
            AuctionId = auction.Id,
            RequestingUserId = userId
        };
        var handler = new GetAuctionBidsQueryHandler(
            _auctionsRepository, _bidsRepository, _visibilityService);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        var userBid = result.Bids.FirstOrDefault(b => b.UserId == userId);
        Assert.That(userBid, Is.Not.Null);
        Assert.That(userBid!.Amount, Is.EqualTo(110));
        Assert.That(userBid.AmountHidden, Is.False);
    }

    [Test]
    public async Task BlindAuction_Active_HighestBidShouldBeHidden()
    {
        // Arrange
        var auction = await CreateActiveBlindAuction();
        var user1 = Guid.NewGuid();
        await SetupUserBalance(user1, 10000);
        await PlaceBid(auction.Id, user1, 110);

        // Act
        var query = new GetAuctionDetailsQuery
        {
            AuctionId = auction.Id,
            RequestingUserId = null
        };
        var handler = new GetAuctionDetailsQueryHandler(
            _auctionsRepository, _bidsRepository, _visibilityService);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.That(result.Auction!.CurrentHighestBid, Is.Null);
    }

    [Test]
    public async Task BlindAuction_Active_BidCountShouldBeVisible()
    {
        // Arrange
        var auction = await CreateActiveBlindAuction();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        await SetupUserBalance(user1, 10000);
        await SetupUserBalance(user2, 10000);
        await PlaceBid(auction.Id, user1, 110);
        await PlaceBid(auction.Id, user2, 150);

        // Act
        var query = new GetAuctionDetailsQuery
        {
            AuctionId = auction.Id,
            RequestingUserId = null
        };
        var handler = new GetAuctionDetailsQueryHandler(
            _auctionsRepository, _bidsRepository, _visibilityService);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.That(result.Auction!.BidCount, Is.EqualTo(2));
    }

    [Test]
    public async Task BlindAuction_Ended_AllBidsShouldBecomeVisible()
    {
        // Arrange
        var auction = await CreateActiveBlindAuction();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        await SetupUserBalance(user1, 10000);
        await SetupUserBalance(user2, 10000);
        await PlaceBid(auction.Id, user1, 110);
        await PlaceBid(auction.Id, user2, 150);

        // End the auction
        auction.TransitionToEnded();
        await _auctionsRepository.UpdateAuction(auction);

        // Act
        var query = new GetAuctionBidsQuery
        {
            AuctionId = auction.Id,
            RequestingUserId = null
        };
        var handler = new GetAuctionBidsQueryHandler(
            _auctionsRepository, _bidsRepository, _visibilityService);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.That(result.Bids.Count, Is.EqualTo(2));
        Assert.That(result.Bids.All(b => b.Amount.HasValue), Is.True);
        Assert.That(result.Bids.All(b => !b.AmountHidden), Is.True);
    }

    [Test]
    public async Task BlindAuction_WithShowMinPriceFalse_MinPriceShouldBeHidden()
    {
        // Arrange
        var auction = await CreateActiveBlindAuction(minPrice: 500, showMinPrice: false);

        // Act
        var query = new GetAuctionDetailsQuery
        {
            AuctionId = auction.Id,
            RequestingUserId = null
        };
        var handler = new GetAuctionDetailsQueryHandler(
            _auctionsRepository, _bidsRepository, _visibilityService);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.That(result.Auction!.MinPrice, Is.Null);
    }

    [Test]
    public async Task BlindAuction_WithShowMinPriceTrue_MinPriceShouldBeVisible()
    {
        // Arrange
        var auction = await CreateActiveBlindAuction(minPrice: 500, showMinPrice: true);

        // Act
        var query = new GetAuctionDetailsQuery
        {
            AuctionId = auction.Id,
            RequestingUserId = null
        };
        var handler = new GetAuctionDetailsQueryHandler(
            _auctionsRepository, _bidsRepository, _visibilityService);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.That(result.Auction!.MinPrice, Is.EqualTo(500));
    }

    private async Task<Auction> CreateActiveOpenAuction(decimal minPrice = 100, decimal increment = 10)
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test Open auction",
            EndTime = _timeProvider.Now().AddHours(1),
            Type = AuctionType.Open,
            MinimumIncrement = increment,
            MinPrice = minPrice,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });
        auction.TransitionToActive();
        await _auctionsRepository.UpdateAuction(auction);
        return auction;
    }

    private async Task<Auction> CreateActiveBlindAuction(
        decimal minPrice = 100, 
        bool showMinPrice = false)
    {
        var auction = await _auctionsRepository.CreateAuction(new Auction
        {
            Title = "Test Blind auction",
            EndTime = _timeProvider.Now().AddHours(1),
            Type = AuctionType.Blind,
            MinPrice = minPrice,
            ShowMinPrice = showMinPrice,
            TieBreakingPolicy = TieBreakingPolicy.Earliest
        });
        auction.TransitionToActive();
        await _auctionsRepository.UpdateAuction(auction);
        return auction;
    }

    private async Task<Bid> PlaceBid(Guid auctionId, Guid userId, decimal amount)
    {
        return await _bidsRepository.CreateBid(new Bid
        {
            AuctionId = auctionId,
            UserId = userId,
            Amount = amount,
            PlacedAt = _timeProvider.Now()
        });
    }

    private async Task SetupUserBalance(Guid userId, decimal amount)
    {
        await _balanceRepository.DepositFunds(userId, amount);
    }

    private PlaceBidCommandHandler CreatePlaceBidHandler()
    {
        var validators = new List<IBidValidator>
        {
            new MaxBidAmountValidator(_biddingConfig),
            new BalanceRatioValidator(_biddingConfig, _balanceRepository),
            new OpenAuctionIncrementValidator(_bidsRepository),
            new BlindAuctionSingleBidValidator(_bidsRepository)
        };

        return new PlaceBidCommandHandler(
            _auctionsRepository,
            _bidsRepository,
            _timeProvider,
            validators);
    }
}