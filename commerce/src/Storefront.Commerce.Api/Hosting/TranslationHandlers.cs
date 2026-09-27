using System.Globalization;
using System.Text.RegularExpressions;
using FluentValidation;
using MPCore.Application.Messaging;
using MPCore.Application.Results;
using MPCore.Audit;
using MPCore.Localization;
using MPCore.Persistence.Abstractions;

namespace Storefront.Commerce.Api.Hosting;

// Translation administration is a host concern, not a bounded context: the texts belong to every
// module's failures. Support edits them here; the product owns the policy of who may.

/// <summary>Stores a translation of a message key in one language, overriding the resource file's text.</summary>
public sealed record SetTranslation(string Key, string Culture, string Text) : ICommand<Result<TranslationView>>;

/// <summary>Removes a stored translation, so the resource file's text applies again.</summary>
public sealed record RemoveTranslation(string Key, string Culture) : ICommand<Result>;

/// <summary>Lists stored translations, optionally of one language.</summary>
public sealed record ListTranslations(string? Culture) : IQuery<Result<IReadOnlyList<MessageTranslationEntry>>>;

public sealed record TranslationView(string Key, string Culture, string Text);

public static class TranslationFailures
{
    public const string Domain = "storefront.localization";

    public static FailureDescriptor UnknownMessageKey(string key) => new(
        new ErrorIdentity(Domain, "UNKNOWN_MESSAGE_KEY"), ErrorCategory.Validation,
        new FailureMessageDescriptor("localization.unknown_message_key", new Dictionary<string, string> { ["key"] = key }),
        RetryDirective.Never,
        [new ValidationFailureDetail([new FieldViolation("key", "UNKNOWN",
            new FailureMessageDescriptor("localization.unknown_message_key", new Dictionary<string, string> { ["key"] = key }))])]);

    public static FailureDescriptor TranslationNotFound() => new(
        new ErrorIdentity(Domain, "TRANSLATION_NOT_FOUND"), ErrorCategory.NotFound,
        new FailureMessageDescriptor("localization.translation_not_found"));
}

public static class TranslationsHandler
{
    public static async Task<Result<TranslationView>> Handle(
        SetTranslation command,
        IMessageTranslationStore store,
        IMessageCatalog catalog,
        IBusinessAuditRecorder audit,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(catalog);

        // An administrator can only translate a key the code uses; the catalog knows which those are.
        if (!catalog.IsKnownKey(command.Key))
        {
            return Result<TranslationView>.FromFailure(TranslationFailures.UnknownMessageKey(command.Key));
        }

        var culture = CultureInfo.GetCultureInfo(command.Culture).Name;
        await store.SetAsync(command.Key, culture, command.Text, cancellationToken).ConfigureAwait(false);
        await audit.RecordAsync(
            "localization", "translation-set", "MessageTranslation", $"{culture}:{command.Key}",
            new Dictionary<string, string> { ["text"] = command.Text }, cancellationToken).ConfigureAwait(false);
        return Result<TranslationView>.Success(new TranslationView(command.Key, culture, command.Text));
    }

    public static async Task<Result> Handle(
        RemoveTranslation command,
        IMessageTranslationStore store,
        IBusinessAuditRecorder audit,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var culture = CultureInfo.GetCultureInfo(command.Culture).Name;
        if (!await store.RemoveAsync(command.Key, culture, cancellationToken).ConfigureAwait(false))
        {
            return Result.FromFailure(TranslationFailures.TranslationNotFound());
        }

        await audit.RecordAsync("localization", "translation-removed", "MessageTranslation", $"{culture}:{command.Key}",
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public static async Task<Result<IReadOnlyList<MessageTranslationEntry>>> Handle(
        ListTranslations query, IMessageTranslationStore store, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Result<IReadOnlyList<MessageTranslationEntry>>.Success(
            await store.ListAsync(query.Culture, cancellationToken).ConfigureAwait(false));
    }
}

/// <summary>The shape of a translation request; the store refuses anything malformed as well.</summary>
public sealed partial class SetTranslationValidator : AbstractValidator<SetTranslation>
{
    public SetTranslationValidator()
    {
        RuleFor(x => x.Key).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(FailureMessageDescriptor.MaximumKeyLength)
            .Must(static key => MessageKey().IsMatch(key)).WithErrorCode("KEY_INVALID").WithMessage("localization.key_invalid");
        RuleFor(x => x.Culture).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(16)
            .Must(IsKnownCulture).WithErrorCode("CULTURE_UNKNOWN").WithMessage("localization.culture_unknown");
        RuleFor(x => x.Text).NotEmpty().MaximumLength(2000);
    }

    internal static bool IsKnownCulture(string culture)
    {
        try
        {
            return CultureInfo.GetCultureInfo(culture, predefinedOnly: true).Name.Length > 0;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*(?:\\.[a-z][a-z0-9_]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex MessageKey();
}

public sealed class RemoveTranslationValidator : AbstractValidator<RemoveTranslation>
{
    public RemoveTranslationValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(FailureMessageDescriptor.MaximumKeyLength);
        RuleFor(x => x.Culture).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(16)
            .Must(SetTranslationValidator.IsKnownCulture).WithErrorCode("CULTURE_UNKNOWN").WithMessage("localization.culture_unknown");
    }
}
