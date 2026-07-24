namespace CreditFlow.Application;

public sealed class PlanRenewalOptions
{
    public const string SectionName = "PlanRenewal";

    public int IntervalSeconds { get; set; } = 60;
}