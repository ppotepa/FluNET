using FluNET.Language;
using FluNET.Prompt;
using FluNET.Sentences;
using FluNET.Syntax.Validation;
using FluNET.Tokens.Tree;

namespace FluNET.Compatibility;

/// <summary>Result of projecting canonical prompt syntax into the legacy sentence model.</summary>
public sealed record LegacySentenceAdaptation(
    ValidationResult ValidationResult,
    ISentence? Sentence)
{
    public bool IsValid => ValidationResult.IsValid && Sentence is not null;
}

/// <summary>
/// Isolates the pre-0.3 TokenTree/SentenceValidator/ISentence model. This
/// adapter validates and projects compatibility state only; it never executes
/// a command or participates in the canonical execution pipeline.
/// </summary>
public sealed class LegacySentenceAdapter
{
    private readonly TokenTreeFactory _tokenTrees;
    private readonly SentenceValidator _validator;
    private readonly SentenceFactory _sentences;
    private readonly LanguageSnapshot _language;

    public LegacySentenceAdapter(
        TokenTreeFactory tokenTrees,
        SentenceValidator validator,
        SentenceFactory sentences,
        LanguageSnapshot language)
    {
        _tokenTrees = tokenTrees ?? throw new ArgumentNullException(nameof(tokenTrees));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _sentences = sentences ?? throw new ArgumentNullException(nameof(sentences));
        _language = language ?? throw new ArgumentNullException(nameof(language));
    }

    /// <summary>
    /// Creates a legacy sentence view when the supplied program is representable
    /// by the old IVerb/IWord model. Native typed modules are expected to be
    /// rejected by this compatibility projection while remaining valid for
    /// Analyze and ExecuteAsync.
    /// </summary>
    public LegacySentenceAdaptation Adapt(ProcessedPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ProcessedPrompt source = prompt.WithGrammar(_language.Grammar);
        if (!source.IsValid)
        {
            string reason = string.Join(" ", source.Diagnostics.Select(diagnostic =>
                $"{diagnostic.Code}: {diagnostic.Message}"));
            return new LegacySentenceAdaptation(
                ValidationResult.Failure(reason),
                null);
        }

        try
        {
            IReadOnlyList<TokenTree> trees = _tokenTrees.ProcessCommands(source);
            ValidationResult validation = _validator.ValidateCommands(trees);
            if (!validation.IsValid)
            {
                return new LegacySentenceAdaptation(validation, null);
            }

            ISentence? sentence = _sentences.CreateFromTrees(trees);
            return sentence is null
                ? new LegacySentenceAdaptation(
                    ValidationResult.Failure("Could not create a legacy sentence from the prompt."),
                    null)
                : new LegacySentenceAdaptation(validation, sentence);
        }
        catch (PromptSyntaxException exception)
        {
            return new LegacySentenceAdaptation(
                ValidationResult.Failure(exception.Message),
                null);
        }
    }
}
