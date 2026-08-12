namespace FluNET.Language;

internal static class LanguageIdentifier
{
    public static string Normalize(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty language identifier is required.", parameterName);
        }

        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                $"Language identifier '{value}' cannot contain whitespace.",
                parameterName);
        }

        return normalized;
    }
}

/// <summary>Stable identity of a command, independent of CLR implementation names.</summary>
public readonly record struct CommandId
{
    public CommandId(string value) => Value = LanguageIdentifier.Normalize(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Stable identity of one semantic command frame.</summary>
public readonly record struct FrameId
{
    public FrameId(string value) => Value = LanguageIdentifier.Normalize(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Stable identity of the module that owns a language declaration.</summary>
public readonly record struct ModuleId
{
    public ModuleId(string value) => Value = LanguageIdentifier.Normalize(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Stable language contract version attached to an immutable snapshot.</summary>
public readonly record struct LanguageVersion
{
    public LanguageVersion(string value) => Value = LanguageIdentifier.Normalize(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Identifiers used by the built-in language during the 0.3 architecture milestone.</summary>
public static class StandardLanguageIdentity
{
    public static ModuleId Module { get; } = new("flunet.core");
    public static LanguageVersion Version { get; } = new("0.3");
}
