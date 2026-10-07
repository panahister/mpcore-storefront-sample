using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Storefront.Commerce.Api.Resources;
using Storefront.Commerce.Modules.Basket.Resources;
using Storefront.Commerce.Modules.Catalog.Domain;
using Storefront.Commerce.Modules.Catalog.Resources;
using Storefront.Commerce.Modules.Ordering.Resources;
using Storefront.Commerce.Modules.Payments.Resources;
using Storefront.Commerce.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using MPCore.Application.Results;
using MPCore.Domain.Rules;
using MPCore.Localization;

namespace Storefront.Commerce.Tests.Unit;

/// <summary>
/// No sentence is written in code: a rule, a validator and a returned failure carry a message key, and the
/// catalog renders it in the caller's language. A key without a text would reach the caller as a problem
/// document with no <c>detail</c>, so every key the code uses must have one, in every language shipped.
/// </summary>
[Trait("Category", "Unit")]
public sealed partial class MessageCatalogTests
{
    private static IMessageCatalog Catalog()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMPCoreMessageCatalog(catalog => catalog
            .AddResources<CatalogMessages>()
            .AddResources<BasketMessages>()
            .AddResources<OrderingMessages>()
            .AddResources<PaymentsMessages>()
            .AddResources<HostMessages>());
        return services.BuildServiceProvider().GetRequiredService<IMessageCatalog>();
    }

    private static string SourceRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Storefront.Commerce.Backend.sln")))
            {
                return Path.Combine(directory.FullName, "src");
            }
        }

        throw new InvalidOperationException("Storefront.Commerce.Backend.sln not found above the test output.");
    }

    [Fact]
    public void A_broken_rule_renders_in_every_language_with_its_arguments()
    {
        var product = new FakeProducts().Seed("TNT-1", 1_000m, 10);
        var broken = Assert.Throws<BusinessRuleValidationException>(() => product.ChangePrice(Price.Of(5_000m), FakeClock.At2026().UtcNow));
        var message = new FailureMessageDescriptor(broken.Rule.MessageKey, broken.Rule.MessageArguments);
        var catalog = Catalog();

        Assert.Equal("A price may move by at most 50% in one step (from 1000.00 to 5000.00).", catalog.Localize(message, CultureInfo.GetCultureInfo("en")));
        // zh-Hans is the parent of zh-CN: the culture a transport negotiates for a caller who asks for zh-CN.
        Assert.Equal("价格单次最多只能变动 50%（从 1000.00 到 5000.00）。", catalog.Localize(message, CultureInfo.GetCultureInfo("zh-Hans")));
        Assert.Equal("يمكن تغيير السعر بنسبة لا تتجاوز 50% في خطوة واحدة (من 1000.00 إلى 5000.00).", catalog.Localize(message, CultureInfo.GetCultureInfo("ar")));
        Assert.Equal(catalog.Localize(message, CultureInfo.GetCultureInfo("ar")), catalog.Localize(message, CultureInfo.GetCultureInfo("ar-SA")));
    }

    [Fact]
    public void A_language_the_store_does_not_speak_falls_back_to_english()
    {
        var message = new FailureMessageDescriptor("basket.empty");
        var catalog = Catalog();

        Assert.Equal("您的购物篮是空的。", catalog.Localize(message, CultureInfo.GetCultureInfo("zh-CN")));
        Assert.Equal(catalog.Localize(message, CultureInfo.GetCultureInfo("en")), catalog.Localize(message, CultureInfo.GetCultureInfo("de-DE")));
    }

    [Fact]
    public void Every_message_key_the_code_uses_has_a_default_text()
    {
        var catalog = Catalog();
        var used = Directory.EnumerateFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(static path => KeyLiteral().Matches(File.ReadAllText(path)).Select(static match => match.Groups[1].Value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(used);
        Assert.All(used, key => Assert.True(catalog.IsKnownKey(key), $"no default text for message key {key}"));
    }

    [Fact]
    public void Every_language_file_translates_exactly_the_default_keys()
    {
        var files = Directory.EnumerateFiles(SourceRoot(), "*Messages.resx", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(5, files.Count);

        foreach (var defaults in files)
        {
            // Preserve published Chinese and add Arabic; neither may silently fall back to English.
            var chinese = defaults[..^".resx".Length] + ".zh-Hans.resx";
            var arabic = defaults[..^".resx".Length] + ".ar.resx";
            Assert.True(File.Exists(chinese), $"missing {chinese}");
            Assert.True(File.Exists(arabic), $"missing {arabic}");
            var translations = Directory.EnumerateFiles(Path.GetDirectoryName(defaults)!, Path.GetFileNameWithoutExtension(defaults) + ".*.resx").ToList();
            Assert.Contains(chinese, translations);
            Assert.All(translations, translation => Assert.Equal(Keys(defaults), Keys(translation)));
            var original = Texts(defaults);
            foreach (var translation in translations)
            {
                foreach (var (key, text) in Texts(translation))
                {
                    Assert.False(string.IsNullOrWhiteSpace(text), $"empty {key} in {translation}");
                    Assert.Equal(Arguments(original[key]), Arguments(text));
                }
            }
        }
    }

    private static List<string> Keys(string file) =>
        [.. XDocument.Load(file).Root!.Elements("data").Select(static data => (string)data.Attribute("name")!).Order(StringComparer.Ordinal)];

    private static Dictionary<string, string> Texts(string file) => XDocument.Load(file).Root!.Elements("data")
        .ToDictionary(static data => (string)data.Attribute("name")!, static data => data.Element("value")!.Value, StringComparer.Ordinal);

    private static List<string> Arguments(string text) =>
        [.. Placeholder().Matches(text).Select(static match => match.Groups[1].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    [GeneratedRegex("\\{(\\w+)\\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex("\"((?:catalog|basket|ordering|payments|localization)\\.[a-z][a-z0-9_]*(?:\\.[a-z][a-z0-9_]*)*)\"")]
    private static partial Regex KeyLiteral();
}
