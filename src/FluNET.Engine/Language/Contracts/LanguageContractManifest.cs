using System.Text.Json;

namespace FluNET.Language.Contracts;

public sealed record LanguageFrameContract(
    string FrameId,
    string UsageName,
    string ResultTypeId,
    string ResultTypeName);

public sealed record LanguageTypeContract(
    string TypeId,
    string Name,
    string Kind,
    string Nullability);

public sealed record LanguageSeparatorContract(
    string Token,
    string Meaning,
    bool ImposesOrdering);

public sealed record LanguageContractManifest(
    string PublicVersion,
    IReadOnlyList<LanguageFrameContract> Frames,
    IReadOnlyList<LanguageTypeContract> Types,
    IReadOnlyList<LanguageSeparatorContract> Separators,
    IReadOnlyList<string> ExpressionPrecedence)
{
    public static LanguageContractManifest Create(
        LanguageSnapshot language,
        LanguageVersion publicVersion)
    {
        ArgumentNullException.ThrowIfNull(language);

        LanguageFrameContract[] frames = language.Commands
            .SelectMany(command => command.Frames)
            .OrderBy(frame => frame.Id.Value, StringComparer.Ordinal)
            .Select(frame => new LanguageFrameContract(
                frame.Id.Value,
                frame.UsageName,
                frame.ResultTypeSymbol.Id.Value,
                frame.ResultTypeSymbol.Name))
            .ToArray();

        TypeSymbol[] builtIns =
        [
            language.Types.Unit,
            language.Types.Text,
            language.Types.Boolean,
            language.Types.Number,
            language.Types.File,
            language.Types.Directory,
            language.Types.Uri,
            language.Types.Json,
            language.Types.Object
        ];

        LanguageTypeContract[] types = builtIns
            .Concat(language.Commands.SelectMany(command => command.Frames).Select(frame => frame.ResultTypeSymbol))
            .GroupBy(type => type.Id.Value, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(type => type.Id.Value, StringComparer.Ordinal)
            .Select(type => new LanguageTypeContract(
                type.Id.Value,
                type.Name,
                type.Kind.ToString(),
                type.Nullability.ToString()))
            .ToArray();

        LanguageSeparatorContract[] separators =
        [
            new(",", "another value/member of the same syntactic role", false),
            new(";", "neutral compact statement boundary", false),
            new("newline", "neutral compact statement boundary", false),
            new("|", "pipeline dataflow from producer to consumer", true),
            new("AND", "explicit canonical parallel coordination", false),
            new("THEN", "explicit canonical ordering/barrier", true)
        ];

        string[] precedence =
        [
            "OR",
            "AND",
            "== !=",
            "< <= > >=",
            "+ -",
            "* /",
            "NOT ! unary-",
            "postfix property/index",
            "primary"
        ];

        return new(publicVersion.Value, frames, types, separators, precedence);
    }

    public string ToJson(bool indented = true) => JsonSerializer.Serialize(
        this,
        new JsonSerializerOptions { WriteIndented = indented });
}
