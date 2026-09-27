using MPCore.Domain.Model;

namespace Storefront.Commerce.Modules.Catalog.Domain;

/// <summary>The Catalog's memory of what it decided for one order.</summary>
/// <remarks>
/// <para>
/// The identity is the order's identity. That single choice is what makes every stock message
/// idempotent: a redelivered <c>ReserveStock</c> finds the decision already taken and repeats the
/// answer instead of reserving twice; a redelivered <c>ReleaseStock</c> finds the reservation already
/// released and does nothing.
/// </para>
/// <para>
/// A rejection is remembered too. Otherwise a redelivery after somebody restocked would reserve for
/// an order that has already been told "out of stock" and cancelled.
/// </para>
/// </remarks>
public sealed class StockReservation : AggregateRoot<Guid>
{
    private readonly List<ReservationLine> lines = [];

    private StockReservation()
    {
    }

    private StockReservation(Guid orderId, ReservationStatus status, IEnumerable<ReservationLine> reservationLines, DateTimeOffset now)
        : base(orderId)
    {
        Status = status;
        lines.AddRange(reservationLines);
        DecidedOnUtc = now;
    }

    public Guid OrderId => Id;

    public ReservationStatus Status { get; private set; }

    public IReadOnlyList<ReservationLine> Lines => lines;

    public DateTimeOffset DecidedOnUtc { get; private set; }

    public DateTimeOffset? ClosedOnUtc { get; private set; }

    public bool IsHeld => Status == ReservationStatus.Held;

    public static StockReservation Hold(Guid orderId, IEnumerable<ReservationLine> reservationLines, DateTimeOffset now) =>
        new(orderId, ReservationStatus.Held, reservationLines, now);

    public static StockReservation Reject(Guid orderId, IEnumerable<ReservationLine> reservationLines, DateTimeOffset now) =>
        new(orderId, ReservationStatus.Rejected, reservationLines, now);

    /// <summary>A release that arrived before its reservation: remember it, so the late request is refused.</summary>
    public static StockReservation Void(Guid orderId, DateTimeOffset now)
    {
        var reservation = new StockReservation(orderId, ReservationStatus.Released, [], now);
        reservation.ClosedOnUtc = now;
        return reservation;
    }

    public void MarkReleased(DateTimeOffset now)
    {
        Status = ReservationStatus.Released;
        ClosedOnUtc = now;
    }

    public void MarkCommitted(DateTimeOffset now)
    {
        Status = ReservationStatus.Committed;
        ClosedOnUtc = now;
    }
}
