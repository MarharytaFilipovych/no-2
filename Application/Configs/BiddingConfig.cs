namespace Application.Configs;

public interface IBiddingConfig
{
    decimal MaxBidAmount { get; }
    decimal BalanceRatioLimit { get; }
}

public class BiddingConfig : IBiddingConfig
{
    public decimal MaxBidAmount { get; set; }
    public decimal BalanceRatioLimit { get; set; }
}
