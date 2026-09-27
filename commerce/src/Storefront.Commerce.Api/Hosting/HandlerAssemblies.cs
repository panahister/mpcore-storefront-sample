using System.Reflection;

namespace Storefront.Commerce.Api.Hosting;

/// <summary>
/// The assemblies whose message handlers and validators this host owns. Discovery is explicit: an
/// assembly that is not listed here is never scanned. Wolverine additionally discovers handlers in this
/// host assembly itself (the Kafka consumer seam and the translation administration), which Program.cs
/// names as <c>ApplicationAssembly</c>.
/// </summary>
public static class HandlerAssemblies
{
    public static IReadOnlyList<Assembly> All { get; } =
    [
        // One line per module, next to that module's AddXModule registration.
        Storefront.Commerce.Modules.Catalog.AssemblyReference.Assembly,
        Storefront.Commerce.Modules.Basket.AssemblyReference.Assembly,
        Storefront.Commerce.Modules.Ordering.AssemblyReference.Assembly,
        Storefront.Commerce.Modules.Payments.AssemblyReference.Assembly,
    ];
}
