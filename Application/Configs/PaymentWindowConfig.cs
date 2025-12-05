namespace Application.Configs;

public interface IPaymentWindowConfig
{
    TimeSpan PaymentDeadline { get; }
    int BanDurationDays { get; }
}

public class PaymentWindowConfig : IPaymentWindowConfig
{
    public TimeSpan PaymentDeadline { get; set; } = TimeSpan.FromHours(3);
    public int BanDurationDays { get; set; } = 7;
}