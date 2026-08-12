using FluNET.Keywords;
using FluNET.Syntax.Core;
using FluNET.Syntax.Validation;

namespace FluNET.Syntax.Verbs;

/// <summary>Compatibility word shape for value-producing SET frames.</summary>
public abstract class Set<TValue> : IVerb, IKeyword
{
    public string Text => "SET";
    public IWord? Next { get; set; }
    public IWord? Previous { get; set; }
    public virtual string[] Synonyms => Array.Empty<string>();
    public bool Validate(IWord word) => true;
    public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon) =>
        ValidationResult.Success();
}

public sealed class SetText : Set<string>
{
}

public sealed class SetJson : Set<System.Text.Json.JsonElement>
{
}

public sealed class SetNumber : Set<decimal>
{
}

public sealed class SetBoolean : Set<bool>
{
}

/// <summary>Compatibility word shape for parsing structured values.</summary>
public abstract class Parse<TValue> : IVerb, IKeyword
{
    public string Text => "PARSE";
    public IWord? Next { get; set; }
    public IWord? Previous { get; set; }
    public virtual string[] Synonyms => Array.Empty<string>();
    public bool Validate(IWord word) => true;
    public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon) =>
        ValidationResult.Success();
}

public sealed class ParseJson : Parse<System.Text.Json.JsonElement>
{
}

/// <summary>Compatibility word shape for rendering typed values.</summary>
public abstract class Format<TValue> : IVerb, IKeyword
{
    public string Text => "FORMAT";
    public IWord? Next { get; set; }
    public IWord? Previous { get; set; }
    public virtual string[] Synonyms => Array.Empty<string>();
    public bool Validate(IWord word) => true;
    public ValidationResult ValidateNext(IWord nextWord, Lexicon.Lexicon lexicon) =>
        ValidationResult.Success();
}

public sealed class FormatJson : Format<string>
{
}
