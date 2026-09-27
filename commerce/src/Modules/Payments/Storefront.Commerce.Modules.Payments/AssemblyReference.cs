using System.Reflection;

namespace Storefront.Commerce.Modules.Payments;

/// <summary>The one name the host uses to list this module for handler and validator discovery.</summary>
public static class AssemblyReference
{
    public static Assembly Assembly { get; } = typeof(AssemblyReference).Assembly;
}
