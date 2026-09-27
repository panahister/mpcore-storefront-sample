namespace Storefront.Commerce.Modules.Payments.Infrastructure;

/// <summary>Where the provider is and who we are to it. Values come from configuration, never from this repository.</summary>
public sealed class DemoPayOptions
{
    public required Uri BaseAddress { get; init; }

    public required string MerchantId { get; init; }
}
