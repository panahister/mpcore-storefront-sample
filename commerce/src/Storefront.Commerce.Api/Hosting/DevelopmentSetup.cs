using Storefront.Commerce.Infrastructure.Persistence;
using Storefront.Commerce.Modules.Catalog.Application.Commands;
using Storefront.Commerce.Modules.Catalog.Application.Views;
using MPCore.Application.Results;
using MPCore.Security;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Storefront.Commerce.Api.Hosting;

/// <summary>
/// Development conveniences, so a developer with Docker Desktop reaches a working storefront with one
/// command. Nothing here runs outside the Development environment.
/// </summary>
public static class DevelopmentSetup
{
    /// <summary>
    /// Applies pending EF Core migrations before the host starts serving, when
    /// <c>Database:MigrateOnStartup</c> is true. A production deployment applies migrations as a
    /// separate, reviewed step instead (<c>dotnet ef database update</c> or a migration bundle).
    /// </summary>
    public static async Task MigrateIfRequestedAsync(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue("Database:MigrateOnStartup", false))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database;
        var pending = (await database.GetPendingMigrationsAsync().ConfigureAwait(false)).ToList();
        if (pending.Count > 0)
        {
            app.Logger.LogInformation("Applying {Count} migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
            await database.MigrateAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Lists the sample catalog once the host has started, when <c>Database:SeedCatalog</c> is true and
    /// the catalog is empty. The products go through the real <see cref="ListProduct"/> command and its
    /// handler — validation, audit and all — under a named system actor.
    /// </summary>
    public static void SeedCatalogWhenStarted(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue("Database:SeedCatalog", false))
        {
            return;
        }

        app.Lifetime.ApplicationStarted.Register(() => _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                if (await context.Set<Modules.Catalog.Domain.Product>().AnyAsync().ConfigureAwait(false))
                {
                    return;
                }

                using var actor = SystemActorScope.Enter("catalog-seed");
                var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
                foreach (var product in SampleCatalog.Products)
                {
                    var result = await bus.InvokeAsync<Result<ProductStockView>>(product).ConfigureAwait(false);
                    if (result.IsFailure)
                    {
                        app.Logger.LogWarning("Seeding {Sku} failed: {Code}", product.Sku, result.FailureDescriptor!.Identity.Code);
                    }
                }

                app.Logger.LogInformation("Seeded the sample catalog: {Count} products", SampleCatalog.Products.Count);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                app.Logger.LogError(exception, "Seeding the sample catalog failed");
            }
        }));
    }
}

/// <summary>Storefront's opening range. Prices in dollars.</summary>
public static class SampleCatalog
{
    /// <summary>The products.</summary>
    public static IReadOnlyList<ListProduct> Products { get; } =
    [
        new("TNT-ALV-2P", "Ridgeline 2-person tent", "Three-season dome tent, 2.4 kg, two doors and two vestibules.", "tents", "Northface Works", 485.00m, 25, 5),
        new("TNT-DMV-4P", "Summit 4-season expedition tent", "Geodesic four-season tent for high camps; snow flaps and 5 poles.", "tents", "Northface Works", 1_390.00m, 6, 2),
        new("SLP-SAB-M5", "Glacier down sleeping bag (-5°C)", "650-fill down, comfort -5°C, 1.1 kg, regular length.", "sleeping-bags", "Featherline", 620.00m, 18, 4),
        new("SLP-TCH-S10", "Meadow synthetic sleeping bag (+10°C)", "Summer bag, machine washable, packs to 3 litres.", "sleeping-bags", "Featherline", 149.00m, 40, 8),
        new("BPK-ZAG-45", "Traverse 45 L trekking backpack", "Adjustable back system, rain cover, hip-belt pockets.", "backpacks", "Wayfarer", 315.00m, 30, 6),
        new("BPK-DNA-28", "Daybreak 28 L day pack", "Hydration-ready day pack with ventilated back panel.", "backpacks", "Wayfarer", 128.00m, 50, 10),
        new("FTW-ALM-42", "Crag mountaineering boots, size 42", "Crampon-compatible leather boots with a waterproof membrane.", "footwear", "Stonestep", 740.00m, 4, 3),
        new("FTW-KLK-41", "Footpath hiking shoes, size 41", "Low-cut hiking shoes with a grippy lug sole.", "footwear", "Stonestep", 225.00m, 20, 5),
        new("CKG-SHL-STV", "Ember gas stove", "Piezo-ignition canister stove, 1.1 kW, 85 g.", "cooking", "Campfire", 39.00m, 60, 10),
        new("CKG-TTN-POT", "Titanium pot 900 ml", "Ultralight titanium pot with folding handles and lid.", "cooking", "Campfire", 56.00m, 35, 8),
        new("CLT-DRY-JKT", "Squall waterproof shell jacket", "Three-layer shell with pit zips and a helmet-compatible hood.", "clothing", "Snowline", 580.00m, 3, 3),
        new("CLT-MRN-BAS", "Merino base layer", "200 g/m² merino wool long-sleeve base layer.", "clothing", "Snowline", 98.00m, 45, 10),
    ];
}
