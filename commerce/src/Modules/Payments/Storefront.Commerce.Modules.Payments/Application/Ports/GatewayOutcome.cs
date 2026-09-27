namespace Storefront.Commerce.Modules.Payments.Application.Ports;

public enum GatewayOutcome
{
    Approved = 1,

    Declined = 2,

    /// <summary>The provider could not be reached or did not answer properly: worth retrying later.</summary>
    Unavailable = 3
}
