using FluNET.Syntax.Core;
using System.Collections.ObjectModel;

namespace FluNET.Language;

/// <summary>Semantic participation of an argument in a command frame.</summary>
public enum SemanticRole
{
    Theme,
    Output,
    Source,
    Goal,
    Recipient,
    Instrument,
    Method,
    Format
}

public enum SlotDirection
{
    Input,
    Output
}

public enum SlotCardinality
{
    Required,
    Optional,
    Repeated
}

/// <summary>
/// One positional or marked argument in a command frame. Marker is null for
/// the positional subject and contains a surface word such as FROM otherwise.
/// </summary>
public sealed record CommandSlotDescriptor
{
    public CommandSlotDescriptor(
        SemanticRole role,
        Type valueType,
        SlotDirection direction,
        SlotCardinality cardinality,
        string? marker)
    {
        Role = role;
        ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
        Direction = direction;
        Cardinality = cardinality;
        Marker = NormalizeOptional(marker);
    }

    public SemanticRole Role { get; }
    public Type ValueType { get; }
    public SlotDirection Direction { get; }
    public SlotCardinality Cardinality { get; }
    public string? Marker { get; }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}

/// <summary>A typed realization of a command handled by one implementation.</summary>
public sealed record CommandFrameDescriptor
{
    internal CommandFrameDescriptor(
        string usageName,
        Type implementationType,
        Type familyType,
        Type resultType,
        bool isDefault,
        IEnumerable<string> qualifiers,
        IEnumerable<CommandSlotDescriptor> slots)
    {
        UsageName = RequireName(usageName, nameof(usageName));
        ImplementationType = implementationType;
        FamilyType = familyType;
        ResultType = resultType;
        IsDefault = isDefault;
        Qualifiers = qualifiers.Select(NormalizeName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        Slots = slots.ToArray();
    }

    public string UsageName { get; }
    public Type ImplementationType { get; }
    public Type FamilyType { get; }
    public Type ResultType { get; }
    public bool IsDefault { get; }
    public IReadOnlyList<string> Qualifiers { get; }
    public IReadOnlyList<CommandSlotDescriptor> Slots { get; }

    private static string RequireName(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty name is required.", parameterName)
            : value.Trim();

    private static string NormalizeName(string value) =>
        RequireName(value, nameof(value)).ToUpperInvariant();
}

/// <summary>A lexical command and all typed frames that it can evoke.</summary>
public sealed record CommandDescriptor
{
    internal CommandDescriptor(
        string name,
        IEnumerable<string> aliases,
        IEnumerable<CommandFrameDescriptor> frames)
    {
        Name = NormalizeName(name);
        Aliases = aliases.Select(NormalizeName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        Frames = frames.ToArray();
        if (Frames.Count == 0)
        {
            throw new LanguageDefinitionException($"Command '{Name}' must declare at least one frame.");
        }
        Type[] families = Frames.Select(frame => frame.FamilyType).Distinct().ToArray();
        if (families.Length != 1)
        {
            throw new LanguageDefinitionException(
                $"All frames of command '{Name}' must belong to the same verb family.");
        }

        int defaultFrames = Frames.Count(frame => frame.IsDefault);
        if (defaultFrames > 1 || (Frames.Count > 1 && defaultFrames != 1))
        {
            throw new LanguageDefinitionException(
                $"Multi-frame command '{Name}' must declare exactly one default frame.");
        }
    }

    public string Name { get; }
    public IReadOnlyList<string> Aliases { get; }
    public IReadOnlyList<CommandFrameDescriptor> Frames { get; }
    public IEnumerable<string> SurfaceForms => new[] { Name }.Concat(Aliases);

    private static string NormalizeName(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty command name is required.", nameof(value))
            : value.Trim().ToUpperInvariant();
}

public sealed record KeywordDescriptor
{
    internal KeywordDescriptor(string text, Type implementationType)
    {
        Text = string.IsNullOrWhiteSpace(text)
            ? throw new ArgumentException("A non-empty keyword is required.", nameof(text))
            : text.Trim().ToUpperInvariant();
        ImplementationType = implementationType;
    }

    public string Text { get; }
    public Type ImplementationType { get; }
}

/// <summary>An immutable, atomically validated definition of one language version.</summary>
public sealed class LanguageSnapshot
{
    private readonly IReadOnlyDictionary<string, CommandDescriptor> _commandsBySurface;
    private readonly IReadOnlyDictionary<string, KeywordDescriptor> _keywordsBySurface;

    internal LanguageSnapshot(
        IEnumerable<CommandDescriptor> commands,
        IEnumerable<KeywordDescriptor> keywords)
    {
        Commands = commands.OrderBy(command => command.Name, StringComparer.Ordinal).ToArray();
        Keywords = keywords.OrderBy(keyword => keyword.Text, StringComparer.Ordinal).ToArray();

        Dictionary<string, CommandDescriptor> commandIndex = new(StringComparer.OrdinalIgnoreCase);
        foreach (CommandDescriptor command in Commands)
        {
            foreach (string surface in command.SurfaceForms)
            {
                if (!commandIndex.TryAdd(surface, command))
                {
                    throw new LanguageDefinitionException(
                        $"Command surface form '{surface}' belongs to both " +
                        $"'{commandIndex[surface].Name}' and '{command.Name}'.");
                }
            }
        }

        Dictionary<string, KeywordDescriptor> keywordIndex = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeywordDescriptor keyword in Keywords)
        {
            if (commandIndex.ContainsKey(keyword.Text))
            {
                throw new LanguageDefinitionException(
                    $"'{keyword.Text}' is registered as both a command and a keyword.");
            }
            if (!keywordIndex.TryAdd(keyword.Text, keyword))
            {
                throw new LanguageDefinitionException($"Keyword '{keyword.Text}' is registered more than once.");
            }
        }

        _commandsBySurface = new ReadOnlyDictionary<string, CommandDescriptor>(commandIndex);
        _keywordsBySurface = new ReadOnlyDictionary<string, KeywordDescriptor>(keywordIndex);
    }

    public IReadOnlyList<CommandDescriptor> Commands { get; }
    public IReadOnlyList<KeywordDescriptor> Keywords { get; }
    public IEnumerable<string> CommandNames => _commandsBySurface.Keys.Order(StringComparer.OrdinalIgnoreCase);

    public CommandDescriptor? FindCommand(string surfaceForm) =>
        _commandsBySurface.TryGetValue(surfaceForm, out CommandDescriptor? command) ? command : null;

    public KeywordDescriptor? FindKeyword(string surfaceForm) =>
        _keywordsBySurface.TryGetValue(surfaceForm, out KeywordDescriptor? keyword) ? keyword : null;
}

public interface IFluNetModule
{
    void Register(LanguageBuilder language);
}

/// <summary>Collects module declarations and freezes them into a snapshot.</summary>
public sealed class LanguageBuilder
{
    private readonly Dictionary<string, MutableCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<KeywordDescriptor> _keywords = [];

    public CommandFrameBuilder Command<TImplementation, TResult>(string name, string usageName)
        where TImplementation : class, IVerb
    {
        string normalized = NormalizeName(name);
        if (!_commands.TryGetValue(normalized, out MutableCommand? command))
        {
            command = new MutableCommand(normalized);
            _commands.Add(normalized, command);
        }

        Type implementationType = typeof(TImplementation);
        if (command.Frames.Any(frame => frame.ImplementationType == implementationType))
        {
            throw new LanguageDefinitionException(
                $"Implementation '{implementationType.FullName}' is already registered for '{normalized}'.");
        }

        MutableFrame frame = new(
            usageName,
            implementationType,
            FindVerbFamily(implementationType),
            typeof(TResult));
        command.Frames.Add(frame);
        return new CommandFrameBuilder(command, frame);
    }

    public LanguageBuilder Keyword<TKeyword>(string text)
        where TKeyword : class, IWord
    {
        _keywords.Add(new KeywordDescriptor(text, typeof(TKeyword)));
        return this;
    }

    public LanguageBuilder AddModule(IFluNetModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        module.Register(this);
        return this;
    }

    public LanguageSnapshot Build()
    {
        CommandDescriptor[] commands = _commands.Values.Select(command =>
            new CommandDescriptor(
                command.Name,
                command.Aliases,
                command.Frames.Select(frame => new CommandFrameDescriptor(
                    frame.UsageName,
                    frame.ImplementationType,
                    frame.FamilyType,
                    frame.ResultType,
                    frame.IsDefault,
                    frame.Qualifiers,
                    frame.Slots))))
            .ToArray();

        return new LanguageSnapshot(commands, _keywords);
    }

    private static Type FindVerbFamily(Type implementationType)
    {
        Type? baseType = implementationType.BaseType;
        while (baseType is not null && !baseType.IsAbstract && baseType != typeof(object))
        {
            baseType = baseType.BaseType;
        }

        if (baseType is null || baseType == typeof(object))
        {
            throw new LanguageDefinitionException(
                $"Verb '{implementationType.FullName}' must inherit from an abstract verb family.");
        }

        return baseType.IsGenericType ? baseType.GetGenericTypeDefinition() : baseType;
    }

    private static string NormalizeName(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty command name is required.", nameof(value))
            : value.Trim().ToUpperInvariant();

    internal sealed class MutableCommand(string name)
    {
        public string Name { get; } = name;
        public HashSet<string> Aliases { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<MutableFrame> Frames { get; } = [];
    }

    internal sealed class MutableFrame(
        string usageName,
        Type implementationType,
        Type familyType,
        Type resultType)
    {
        public string UsageName { get; } = usageName;
        public Type ImplementationType { get; } = implementationType;
        public Type FamilyType { get; } = familyType;
        public Type ResultType { get; } = resultType;
        public bool IsDefault { get; set; }
        public HashSet<string> Qualifiers { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<CommandSlotDescriptor> Slots { get; } = [];
    }

    public sealed class CommandFrameBuilder
    {
        private readonly MutableCommand _command;
        private readonly MutableFrame _frame;

        internal CommandFrameBuilder(MutableCommand command, MutableFrame frame)
        {
            _command = command;
            _frame = frame;
        }

        public CommandFrameBuilder Aliases(params string[] aliases)
        {
            foreach (string alias in aliases)
            {
                _command.Aliases.Add(NormalizeName(alias));
            }
            return this;
        }

        public CommandFrameBuilder Qualifiers(params string[] qualifiers)
        {
            foreach (string qualifier in qualifiers)
            {
                _frame.Qualifiers.Add(NormalizeName(qualifier));
            }
            return this;
        }

        public CommandFrameBuilder Default()
        {
            if (_command.Frames.Any(frame => frame != _frame && frame.IsDefault))
            {
                throw new LanguageDefinitionException(
                    $"Command '{_command.Name}' already has a default frame.");
            }

            _frame.IsDefault = true;
            return this;
        }

        public CommandFrameBuilder Positional<TValue>(
            SemanticRole role,
            SlotDirection direction = SlotDirection.Input,
            SlotCardinality cardinality = SlotCardinality.Required) =>
            AddSlot<TValue>(role, null, direction, cardinality);

        public CommandFrameBuilder Marked<TValue>(
            SemanticRole role,
            string marker,
            SlotCardinality cardinality = SlotCardinality.Required,
            SlotDirection direction = SlotDirection.Input) =>
            AddSlot<TValue>(role, marker, direction, cardinality);

        private CommandFrameBuilder AddSlot<TValue>(
            SemanticRole role,
            string? marker,
            SlotDirection direction,
            SlotCardinality cardinality)
        {
            if (_frame.Slots.Any(slot => slot.Role == role))
            {
                throw new LanguageDefinitionException(
                    $"Frame '{_command.Name}/{_frame.UsageName}' declares role '{role}' more than once.");
            }

            _frame.Slots.Add(new CommandSlotDescriptor(
                role,
                typeof(TValue),
                direction,
                cardinality,
                marker));
            return this;
        }
    }
}

public sealed class LanguageDefinitionException : Exception
{
    public LanguageDefinitionException(string message) : base(message)
    {
    }
}
