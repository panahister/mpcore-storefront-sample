using MPCore.Domain.Model;

namespace Storefront.Fulfillment.Domain;

/// <summary>
/// Where the parcel goes and whom the courier calls. A value object in Eric Evans's sense: no identity,
/// compared by its parts, immutable.
/// </summary>
/// <remarks>
/// The warehouse does not decide what a valid address is; the shop did, before it took the order. This
/// context keeps what it was told, and refuses only what it cannot print on a label: a blank part.
/// </remarks>
public sealed class DeliveryAddress : ValueObject
{
    public DeliveryAddress(string recipientName, string phone, string province, string city, string line, string postalCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientName);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);
        ArgumentException.ThrowIfNullOrWhiteSpace(province);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentException.ThrowIfNullOrWhiteSpace(line);
        ArgumentException.ThrowIfNullOrWhiteSpace(postalCode);

        RecipientName = recipientName.Trim();
        Phone = phone.Trim();
        Province = province.Trim();
        City = city.Trim();
        Line = line.Trim();
        PostalCode = postalCode.Trim();
    }

    public string RecipientName { get; }

    public string Phone { get; }

    public string Province { get; }

    public string City { get; }

    public string Line { get; }

    public string PostalCode { get; }

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
