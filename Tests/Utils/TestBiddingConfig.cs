using Application.Configs;

namespace Tests.Utils;

public class TestBiddingConfig : IBiddingConfig
{
    public decimal MaxBidAmount { get; set; }
    public decimal BalanceRatioLimit { get; set; }
}