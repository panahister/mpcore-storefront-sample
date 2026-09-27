using Storefront.Commerce.Modules.Catalog.Contracts;
using Wolverine;
using Wolverine.Runtime;

namespace Storefront.Commerce.Api.Hosting;

/// <summary>
/// What the order process is told when a step of it is given up and its message goes to the error queue.
/// </summary>
/// <remarks>
/// <para>
/// A process manager (Gregor Hohpe and Bobby Woolf, <i>Enterprise Integration Patterns</i>) sends a command
/// and waits for the answer. A command that ends in the error queue answers nothing, and the process waits
/// for ever: the shopper was told "accepted" and nobody is told anything else. So giving a message up is
/// itself an answer, sent here, where the host's error policy makes that decision.
/// </para>
/// <para>
/// The message stays in the error queue for an operator. Replaying it later is harmless: the order is
/// cancelled by then, its stock request voided, and the Catalog repeats "rejected".
/// </para>
/// </remarks>
public static class GivenUpMessages
{
    /// <summary>The description Wolverine shows for this action.</summary>
    public const string Description = "tell the order process that the step was given up";

    /// <summary>The answer for a message that was given up, or null when nobody waits for one.</summary>
    public static object? AnswerFor(object? message) => message switch
    {
        ReserveStock request => new StockReservationAbandoned(request.OrderId),
        _ => null
    };

    /// <summary>Runs with the error policy's decision to give the message up.</summary>
    public static async ValueTask TellTheProcessAsync(IWolverineRuntime runtime, IEnvelopeLifecycle lifecycle, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(lifecycle);

        if (AnswerFor(lifecycle.Envelope?.Message) is { } answer)
        {
            await new MessageBus(runtime).PublishAsync(answer).ConfigureAwait(false);
        }
    }
}
