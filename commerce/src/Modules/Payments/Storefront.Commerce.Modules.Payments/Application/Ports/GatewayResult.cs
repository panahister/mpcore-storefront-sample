namespace Storefront.Commerce.Modules.Payments.Application.Ports;

/// <summary>What the provider said, translated at the boundary so no provider type reaches the application.</summary>
public sealed record GatewayResult(GatewayOutcome Outcome, string? Reference)
{
    public static GatewayResult Approved(string reference) => new(GatewayOutcome.Approved, reference);

    public static GatewayResult Declined(string code) => new(GatewayOutcome.Declined, code);

    public static GatewayResult Unavailable { get; } = new(GatewayOutcome.Unavailable, null);
}
