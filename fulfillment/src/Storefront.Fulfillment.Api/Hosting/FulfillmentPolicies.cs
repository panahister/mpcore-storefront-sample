using Microsoft.AspNetCore.Authorization;
using MPCore.Security.AspNetCore;

namespace Storefront.Fulfillment.Api.Hosting;

/// <summary>
/// The named authorization policies of this host, contributed to MP Core's default-deny authorization.
/// </summary>
/// <remarks>
/// MP Core ships one named policy, any authenticated caller, and a fallback that protects every endpoint
/// without metadata. It ships no product role. The role here is Storefront's, issued by the
/// <c>storefront</c> realm and normalised by MP Core's Keycloak claim mapping.
/// </remarks>
public sealed class FulfillmentPolicies : IMPCoreAuthorizationPolicyContributor
{
    /// <summary>The realm role of the warehouse system and its staff.</summary>
    public const string WarehouseRole = "warehouse";

    /// <summary>The warehouse.</summary>
    public const string Warehouse = "storefront.warehouse";

    /// <inheritdoc />
    public void Contribute(AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.AddPolicy(Warehouse, MPCoreAuthorizationPolicies.RequireRole(WarehouseRole));
    }
}
