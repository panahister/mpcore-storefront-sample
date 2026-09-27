using MPCore.Domain.Model;

namespace Storefront.Commerce.Modules.Ordering.Domain;

/// <summary>
/// Where the order goes. A value object in Eric Evans's sense: no identity, compared by its parts, immutable.
/// It is built from parts that already carry their own rules (<see cref="PhoneNumber"/>,
/// <see cref="PostalCode"/>), so an address that exists is an address the courier can use.
/// </summary>
public sealed class ShippingAddress : ValueObject
{
    public ShippingAddress(string recipientName, PhoneNumber phone, string province, string city, string line, PostalCode postalCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientName);
        ArgumentException.ThrowIfNullOrWhiteSpace(province);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentException.ThrowIfNullOrWhiteSpace(line);

        RecipientName = recipientName.Trim();
        Phone = phone;
        Province = province.Trim();
        City = city.Trim();
        Line = line.Trim();
        PostalCode = postalCode;
    }

    public string RecipientName { get; }

    public PhoneNumber Phone { get; }

    public string Province { get; }

    public string City { get; }

    public string Line { get; }

    public PostalCode PostalCode { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return RecipientName;
        yield return Phone;
        yield return Province;
        yield return City;
        yield return Line;
        yield return PostalCode;
    }
}
