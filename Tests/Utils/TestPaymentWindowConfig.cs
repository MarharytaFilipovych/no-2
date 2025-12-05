namespace Tests.Utils;

using Application.Configs;

public class TestPaymentWindowConfig : IPaymentWindowConfig
{
    public TimeSpan PaymentDeadline { get; set; } = TimeSpan.FromHours(3);
    public int BanDurationDays { get; set; } = 7;
}