using Storefront.Commerce.Modules.Catalog.Application;
using Storefront.Commerce.Modules.Ordering.Application;
using Microsoft.AspNetCore.Authorization;
using MPCore.Security.AspNetCore;

namespace Storefront.Commerce.Api.Hosting;

/// <summary>
/// The named authorization policies of this host, contributed to MP Core's default-deny authorization.
/// </summary>
/// <remarks>
/// <para>
/// MP Core ships one named policy — any authenticated caller — and a fallback that protects every
/// endpoint without metadata. It ships no product role. The roles here are Storefront's, issued by the
/// <c>storefront</c> realm as realm roles and normalised by MP Core's Keycloak claim mapping.
/// </para>
/// <para>
/// A policy answers "may this kind of caller use this endpoint". Whether this caller may act on
/// <i>that</i> order is a business question and stays in the handler (see <c>OrderAccess</c>).
/// </para>
/// </remarks>
public sealed class StorefrontPolicies : IMPCoreAuthorizationPolicyContributor
{
    /// <summary>Shoppers.</summary>
    public const string Customer = "storefront.customer";

    /// <summary>Catalog managers.</summary>
    public const string CatalogManager = "storefront.catalog-manager";

    /// <summary>Customer support.</summary>
    public const string Support = "storefront.support";

    /// <summary>The warehouse system.</summary>
    public const string Warehouse = "storefront.warehouse";

    /// <summary>Anybody who may look at an order: its buyer, support, the warehouse.</summary>
    public const string OrderReaders = "storefront.order-readers";

    /// <summary>Anybody who may cancel an order: its buyer or support.</summary>
    public const string OrderCancellers = "storefront.order-cancellers";

    /// <inheritdoc />
    public void Contribute(AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.AddPolicy(Customer, MPCoreAuthorizationPolicies.RequireRole(OrderingRoles.Customer));
        options.AddPolicy(CatalogManager, MPCoreAuthorizationPolicies.RequireRole(CatalogRoles.Manager));
        options.AddPolicy(Support, MPCoreAuthorizationPolicies.RequireRole(OrderingRoles.Support));
        options.AddPolicy(Warehouse, MPCoreAuthorizationPolicies.RequireRole(OrderingRoles.Warehouse));
        options.AddPolicy(OrderReaders, MPCoreAuthorizationPolicies.RequireRole(
            OrderingRoles.Customer, OrderingRoles.Support, OrderingRoles.Warehouse));
        options.AddPolicy(OrderCancellers, MPCoreAuthorizationPolicies.RequireRole(
            OrderingRoles.Customer, OrderingRoles.Support));
    }
}
