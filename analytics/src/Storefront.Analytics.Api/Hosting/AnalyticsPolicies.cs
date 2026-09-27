using Microsoft.AspNetCore.Authorization;
using MPCore.Security.AspNetCore;

namespace Storefront.Analytics.Api.Hosting;

/// <summary>
/// The named authorization policies of this host, contributed to MP Core's default-deny authorization.
/// </summary>
public sealed class AnalyticsPolicies : IMPCoreAuthorizationPolicyContributor
{
    /// <summary>The realm role of the people who read the figures.</summary>
    public const string AnalystRole = "analyst";

    /// <summary>Analysts.</summary>
    public const string Analyst = "storefront.analyst";

    /// <inheritdoc />
    public void Contribute(AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.AddPolicy(Analyst, MPCoreAuthorizationPolicies.RequireRole(AnalystRole));
    }
}
